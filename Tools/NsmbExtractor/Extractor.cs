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

            HashSet<string> writtenTilesets = new();
            List<AreaIndexEntry> index = new();
            foreach (string name in game.AreaNames) {
                LevelArea area = game.LoadArea(name);
                AreaTilesets tilesets = game.GetTilesets(area);
                LevelGrid grid = game.ExpandArea(area, tilesets);

                // Atlases depend on all three sets, because tiles can use each other's chars and palettes.
                string setKey = $"set_{area.Settings.Tileset:D2}_{tilesets.CommonVariant}";
                WriteTilesetGroup(tilesets, setKey, tilesetsDir, writtenTilesets);

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
