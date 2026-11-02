using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using NsmbRom.Imaging;
using NsmbRom.Nsmb;

namespace NsmbExtractor {
    /// <summary>Writes Unity-importable files: tileset atlases and behaviours, area JSON, Map16 grids and an index.</summary>
    public static class Extractor {
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
        private static readonly Regex AreaName = new(@"^([A-Z])(\d\d)_(\d)$");

        private sealed class AreaIndexEntry {
            public string Name { get; set; } = "";
            public string World { get; set; } = "";
            public int Level { get; set; }
            public int Area { get; set; }
            public string TilesetGroup { get; set; } = "";
            public int Sprites { get; set; }
            public int Entrances { get; set; }
            public int MissingObjects { get; set; }
        }

        public static void Extract(NsmbGame game, string romName, string outDir) {
            string levelsDir = Path.Combine(outDir, "levels");
            string tilesetsDir = Path.Combine(outDir, "tilesets");
            Directory.CreateDirectory(levelsDir);
            Directory.CreateDirectory(tilesetsDir);
            string layersDir = Path.Combine(outDir, "layers");
            if (Directory.Exists(layersDir)) {
                foreach (string stale in Directory.GetFiles(layersDir, "*.png")) {
                    File.Delete(stale);
                }
            }
            Directory.CreateDirectory(layersDir);

            HashSet<string> writtenLayers = new();
            HashSet<string> writtenTilesets = new();
            List<AreaIndexEntry> index = new();
            foreach (string name in game.AreaNames) {
                LevelArea area = game.LoadArea(name);
                AreaTilesets tilesets = game.GetTilesets(area);
                LevelGrid grid = game.ExpandArea(area, tilesets);

                // Atlases depend on all three sets, because tiles can use each other's chars and palettes.
                string setKey = $"set_{area.Settings.Tileset:D2}_{tilesets.CommonVariant}";
                WriteTilesetGroup(tilesets, setKey, tilesetsDir, writtenTilesets);

                WriteLayer(game, true, area.Settings.Background, area.Settings.Foreground, layersDir, writtenLayers);
                WriteLayer(game, false, area.Settings.Background, area.Settings.Foreground, layersDir, writtenLayers);

                File.WriteAllText(Path.Combine(levelsDir, name + ".json"), JsonSerializer.Serialize(area, Json));
                File.WriteAllBytes(Path.Combine(levelsDir, name + ".map16"), MemoryMarshal.AsBytes(grid.Cells.AsSpan()).ToArray());

                Match match = AreaName.Match(name);
                index.Add(new AreaIndexEntry {
                    Name = name,
                    World = match.Groups[1].Value,
                    Level = match.Success ? int.Parse(match.Groups[2].Value) : 0,
                    Area = match.Success ? int.Parse(match.Groups[3].Value) : 0,
                    TilesetGroup = setKey,
                    Sprites = area.Sprites.Count,
                    Entrances = area.Entrances.Count,
                    MissingObjects = grid.MissingObjects,
                });
            }

            File.WriteAllText(Path.Combine(levelsDir, "index.json"), JsonSerializer.Serialize(index, Json));
            File.WriteAllText(Path.Combine(outDir, "manifest.json"), JsonSerializer.Serialize(new {
                Rom = romName,
                GameCode = game.Rom.Header.GameCode,
                Region = game.Region.ToString(),
                Areas = index.Count,
                Tilesets = writtenTilesets.Count,
                ExcludedAreas = NsmbGame.ExcludedAreas,
                GridWidth = LevelGrid.Width,
                GridHeight = LevelGrid.Height,
                AtlasColumns = Tileset.AtlasColumns,
            }, Json));

            Console.WriteLine($"Wrote {index.Count} areas and {writtenTilesets.Count} tilesets to {outDir}");
            Console.WriteLine($"Areas with unresolved objects: {index.Count(e => e.MissingObjects > 0)}");
        }

        public static void Render(NsmbGame game, string outDir) {
            Directory.CreateDirectory(outDir);
            AreaRenderer renderer = new();
            foreach (string name in game.AreaNames) {
                LevelArea area = game.LoadArea(name);
                AreaTilesets tilesets = game.GetTilesets(area);
                renderer.RenderArea(area, game.ExpandArea(area, tilesets), tilesets, Path.Combine(outDir, name + ".png"));
            }
            renderer.WriteContactSheet(Path.Combine(outDir, "_contact.png"));
            Console.WriteLine($"Rendered {game.AreaNames.Count} areas to {outDir}");
        }

        // Named by pair: each layer can show chars of the other one.
        private static void WriteLayer(NsmbGame game, bool background, int backgroundId, int foregroundId, string layersDir, HashSet<string> written) {
            string name = $"{(background ? "bg" : "fg")}_{backgroundId:D2}_{foregroundId:D2}";
            if (!written.Add(name)) {
                return;
            }

            byte[]? rgba = background
                ? game.RenderBackground(backgroundId, foregroundId, out int width, out int height)
                : game.RenderForeground(backgroundId, foregroundId, out width, out height);
            if (rgba != null) {
                using FileStream file = File.Create(Path.Combine(layersDir, name + ".png"));
                PngWriter.Write(file, rgba, width, height);
            }
        }

        private static void WriteTilesetGroup(AreaTilesets tilesets, string key, string tilesetsDir, HashSet<string> written) {
            if (!written.Add(key)) {
                return;
            }

            string dir = Path.Combine(tilesetsDir, key);
            Directory.CreateDirectory(dir);
            int badReferences = 0;
            foreach (TilesetSlot slot in Enum.GetValues<TilesetSlot>()) {
                Tileset tileset = tilesets[slot];
                string name = slot.ToString().ToLowerInvariant();
                byte[] atlas = tileset.RenderAtlas(tilesets, out int width, out int height);
                badReferences += tileset.BadCharReferences;
                using (FileStream file = File.Create(Path.Combine(dir, name + ".png"))) {
                    PngWriter.Write(file, atlas, width, height);
                }
                File.WriteAllBytes(Path.Combine(dir, name + ".behaviors"), MemoryMarshal.AsBytes(tileset.Behaviors.AsSpan()).ToArray());
            }
            File.WriteAllText(Path.Combine(dir, "group.json"), JsonSerializer.Serialize(new { BadCharReferences = badReferences }, Json));
        }
    }

    /// <summary>Checks that every enterable entrance leads to an area and entrance that exist.</summary>
    public static class EntranceGraph {
        private const int ExitOnly = 0x80;
        private const int ConnectedSetting = 0x08;

        public static bool Check(NsmbGame game) {
            int checkedLinks = 0;
            List<string> broken = new();
            foreach (string name in game.AreaNames) {
                LevelArea area = game.LoadArea(name);
                foreach (LevelEntrance entrance in area.Entrances) {
                    bool isWarp = entrance.Type is 2 or (>= 3 and <= 6) or (>= 16 and <= 19) or (>= 22 and <= 25);
                    if (!isWarp || (entrance.Settings & ExitOnly) != 0) {
                        continue;
                    }

                    checkedLinks++;
                    // Connected pipes pair up by number instead of using the destination fields.
                    if ((entrance.Settings & ConnectedSetting) != 0) {
                        int partners = area.Entrances.Count(e => e != entrance && (e.Settings & ConnectedSetting) != 0 && e.ConnectedPipe == entrance.ConnectedPipe);
                        if (partners != 1) {
                            broken.Add($"{name} entrance {entrance.Number}: connected pipe {entrance.ConnectedPipe} has {partners} partners");
                        }
                        continue;
                    }

                    // Destination area 0 means the same area.
                    string target = entrance.DestArea == 0 ? name : $"{name.Substring(0, 4)}{entrance.DestArea}";
                    if (!game.AreaNames.Contains(target)) {
                        broken.Add($"{name} entrance {entrance.Number}: no area {target}");
                    } else if (game.LoadArea(target).Entrances.All(e => e.Number != entrance.DestEntrance)) {
                        broken.Add($"{name} entrance {entrance.Number}: {target} has no entrance {entrance.DestEntrance}");
                    }
                }
            }

            Console.WriteLine($"Checked {checkedLinks} warps, {broken.Count} broken");
            foreach (string line in broken) {
                Console.WriteLine("  " + line);
            }
            return broken.Count == 0;
        }

        /// <summary>Lists the tiles of an area whose behaviour word matches a mask, for placing tests.</summary>
        public static void FindTiles(NsmbGame game, string name, uint mask, TextWriter output) {
            LevelArea area = game.LoadArea(name);
            AreaTilesets tilesets = game.GetTilesets(area);
            LevelGrid grid = game.ExpandArea(area, tilesets);
            for (int y = 0; y < LevelGrid.Height; y++) {
                for (int x = 0; x < LevelGrid.Width; x++) {
                    ushort cell = grid.Cells[(y * LevelGrid.Width) + x];
                    TilesetSlot slot = LevelGrid.SplitCell(cell, out int tile);
                    uint word = cell != 0 && tile < tilesets[slot].Behaviors.Length ? tilesets[slot].Behaviors[tile] : 0;
                    if ((word & mask) != 0) {
                        output.WriteLine($"{x},{y} 0x{word:X8}");
                    }
                }
            }
        }

        /// <summary>Prints the tiles around each warp: # solid, P pipe or door, o entrance cell, and nearby sprites.</summary>
        public static void Surroundings(NsmbGame game, TextWriter output) {
            const int Reach = 4;
            const uint SolidFlag = 0x00010000;
            const uint PipeFlag = 0x01000000;
            const int SpriteReach = 64;

            foreach (string name in game.AreaNames) {
                LevelArea area = game.LoadArea(name);
                AreaTilesets tilesets = game.GetTilesets(area);
                LevelGrid grid = game.ExpandArea(area, tilesets);
                foreach (LevelEntrance entrance in area.Entrances) {
                    if (entrance.Type is not (2 or (>= 3 and <= 6) or 13 or 14 or 15 or (>= 16 and <= 19) or (>= 22 and <= 25))) {
                        continue;
                    }

                    output.WriteLine($"{name} e{entrance.Number} type {entrance.Type} at {entrance.X},{entrance.Y} settings 0x{entrance.Settings:X2}");
                    int column = entrance.X / 16;
                    int row = entrance.Y / 16;
                    for (int y = Math.Max(row - Reach, 0); y <= row + Reach; y++) {
                        char[] line = new char[(2 * Reach) + 1];
                        for (int x = column - Reach; x <= column + Reach; x++) {
                            uint word = 0;
                            if (x >= 0 && x < LevelGrid.Width && y < LevelGrid.Height) {
                                ushort cell = grid.Cells[(y * LevelGrid.Width) + x];
                                TilesetSlot slot = LevelGrid.SplitCell(cell, out int tile);
                                if (cell != 0 && tile < tilesets[slot].Behaviors.Length) {
                                    word = tilesets[slot].Behaviors[tile];
                                }
                            }
                            char mark = (word & PipeFlag) != 0 ? 'P' : (word & SolidFlag) != 0 ? '#' : '.';
                            line[x - column + Reach] = x == column && y == row ? (mark == '.' ? 'o' : char.ToLowerInvariant(mark == '#' ? 'x' : 'p')) : mark;
                        }
                        output.WriteLine("  " + new string(line));
                    }
                    foreach (LevelSprite sprite in area.Sprites) {
                        if (Math.Abs((sprite.X * 16) - entrance.X) <= SpriteReach && Math.Abs((sprite.Y * 16) - entrance.Y) <= SpriteReach) {
                            output.WriteLine($"  sprite {sprite.Type} at {sprite.X},{sprite.Y}");
                        }
                    }
                }
            }
        }
    }

    /// <summary>Usage counts across all areas; this decides the implementation order of actors and tile logic.</summary>
    public static class Census {
        public static void Write(NsmbGame game, string outPath) {
            Dictionary<int, (int Count, HashSet<string> Areas)> sprites = new();
            Dictionary<int, (int Count, HashSet<string> Areas)> entrances = new();
            Dictionary<uint, (int Count, HashSet<string> Areas)> behaviors = new();
            Dictionary<int, (int Count, HashSet<string> Areas)> blankTiles = new();
            int cellsOutOfRange = 0;
            int behaviorlessCells = 0;

            foreach (string name in game.AreaNames) {
                LevelArea area = game.LoadArea(name);
                AreaTilesets tilesets = game.GetTilesets(area);
                LevelGrid grid = game.ExpandArea(area, tilesets);

                foreach (LevelSprite sprite in area.Sprites) {
                    Add(sprites, sprite.Type, name);
                }
                foreach (LevelEntrance entrance in area.Entrances) {
                    Add(entrances, entrance.Type, name);
                }
                foreach (ushort cell in grid.Cells) {
                    if (cell == 0) {
                        continue;
                    }

                    TilesetSlot slot = LevelGrid.SplitCell(cell, out int tile);
                    Tileset tileset = tilesets[slot];
                    if (tile >= tileset.TileCount) {
                        cellsOutOfRange++;
                    } else if (tile >= tileset.Behaviors.Length) {
                        behaviorlessCells++;
                    } else {
                        Add(behaviors, tileset.Behaviors[tile], name);
                        if (tileset.IsBlankTile(tile)) {
                            Add(blankTiles, ((int) slot << 12) | tile, name);
                        }
                    }
                }
            }

            using StreamWriter output = new(outPath);
            output.WriteLine($"Areas: {game.AreaNames.Count}");
            output.WriteLine($"Cells past their tileset: {cellsOutOfRange}");
            output.WriteLine($"Cells without a behaviour entry: {behaviorlessCells}");
            WriteTable(output, "Sprite types", sprites, id => id.ToString());
            WriteTable(output, "Entrance types", entrances, id => id.ToString());
            WriteTable(output, "Tile behaviours", behaviors, id => $"0x{id:X8}");
            WriteTable(output, "Placed tiles with blank Map16 (slot:tile)", blankTiles, id => $"{id >> 12}:0x{id & 0xFFF:X3}");

            Console.WriteLine($"Sprite types used: {sprites.Count}, entrance types: {entrances.Count}, tile behaviours: {behaviors.Count}");
            Console.WriteLine($"Cells past their tileset: {cellsOutOfRange}, without a behaviour entry: {behaviorlessCells}");
            Console.WriteLine($"Placed blank tiles: {blankTiles.Count} kinds, {blankTiles.Values.Sum(v => v.Count)} cells, in {blankTiles.Values.SelectMany(v => v.Areas).Distinct().Count()} areas");
            Console.WriteLine($"Wrote {outPath}");
        }

        private static void Add<T>(Dictionary<T, (int Count, HashSet<string> Areas)> table, T key, string area) where T : notnull {
            if (!table.TryGetValue(key, out (int Count, HashSet<string> Areas) entry)) {
                entry = (0, new HashSet<string>());
            }
            entry.Areas.Add(area);
            table[key] = (entry.Count + 1, entry.Areas);
        }

        private static void WriteTable<T>(StreamWriter output, string title, Dictionary<T, (int Count, HashSet<string> Areas)> table, Func<T, string> format) where T : notnull {
            output.WriteLine();
            output.WriteLine($"{title} ({table.Count}): id, count, areas, first area");
            foreach (KeyValuePair<T, (int Count, HashSet<string> Areas)> row in table.OrderByDescending(r => r.Value.Count)) {
                output.WriteLine($"{format(row.Key),-12} {row.Value.Count,8} {row.Value.Areas.Count,5} {row.Value.Areas.Min(StringComparer.Ordinal)}");
            }
        }
    }

    /// <summary>Reports how two regions' level data differ. Differences are expected, see the plan.</summary>
    public static class RegionVerifier {
        public static void Report(NsmbGame first, NsmbGame second) {
            List<string> onlyFirst = first.AreaNames.Except(second.AreaNames).ToList();
            List<string> onlySecond = second.AreaNames.Except(first.AreaNames).ToList();
            List<string> common = first.AreaNames.Intersect(second.AreaNames).ToList();
            Console.WriteLine($"Areas: {first.AreaNames.Count} vs {second.AreaNames.Count}, {common.Count} in common");
            Console.WriteLine($"Only in first: {string.Join(" ", onlyFirst)}");
            Console.WriteLine($"Only in second: {string.Join(" ", onlySecond)}");

            int different = 0;
            foreach (string name in common) {
                LevelArea a = first.LoadArea(name);
                LevelArea b = second.LoadArea(name);
                LevelGrid gridA = first.ExpandArea(a, first.GetTilesets(a));
                LevelGrid gridB = second.ExpandArea(b, second.GetTilesets(b));
                int cells = gridA.Cells.Zip(gridB.Cells).Count(pair => pair.First != pair.Second);
                int blocks = a.RawBlocks.Zip(b.RawBlocks).Count(pair => pair.First != pair.Second);
                if (cells > 0 || blocks > 0 || a.Objects.Count != b.Objects.Count) {
                    different++;
                    Console.WriteLine($"{name}: {cells} grid cells, {blocks} blocks, objects {a.Objects.Count}/{b.Objects.Count}, sprites {a.Sprites.Count}/{b.Sprites.Count}, entrances {a.Entrances.Count}/{b.Entrances.Count}");
                }
            }
            Console.WriteLine($"Areas that differ: {different} of {common.Count}");
        }
    }
}
