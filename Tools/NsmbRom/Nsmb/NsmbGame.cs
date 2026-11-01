using System;
using System.Collections.Generic;
using System.Linq;
using NsmbRom.Compression;
using NsmbRom.Nds;
using NsmbRom.Nitro;

namespace NsmbRom.Nsmb {
    /// <summary>The expanded Map16 grid of an area. Cell 0 is empty, otherwise the tileset slot in bits 12+ and the Map16 index below.</summary>
    public sealed class LevelGrid {
        public const int Width = 512;
        public const int Height = 256;
        private const int LevelSlotStart = 0x100;
        private const int SubSlotStart = 0x400;
        private const int SlotShift = 12;
        private const int TileMask = (1 << SlotShift) - 1;

        public ushort[] Cells { get; } = new ushort[Width * Height];
        public int MissingObjects { get; internal set; }

        /// <summary>Object data numbers tiles globally across the three tilesets; the extended flag is an offset inside a set.</summary>
        internal static int MakeCell(int globalTile, int extendedOffset) {
            if (globalTile >= SubSlotStart) {
                return ((int) TilesetSlot.Sub << SlotShift) | (globalTile - SubSlotStart + extendedOffset);
            }
            if (globalTile >= LevelSlotStart) {
                return ((int) TilesetSlot.Level << SlotShift) | (globalTile - LevelSlotStart + extendedOffset);
            }
            return globalTile + extendedOffset;
        }

        /// <summary>Splits a cell into the tileset it belongs to and the Map16 index inside that set.</summary>
        public static TilesetSlot SplitCell(int cell, out int tile) {
            tile = cell & TileMask;
            return (TilesetSlot) (cell >> SlotShift);
        }

        internal void Set(int x, int y, ushort cell) {
            if (x >= 0 && x < Width && y >= 0 && y < Height) {
                Cells[(y * Width) + x] = cell;
            }
        }
    }

    /// <summary>The set of tilesets an area draws with.</summary>
    public sealed class AreaTilesets {
        public Tileset Common { get; }
        public Tileset Level { get; }
        public Tileset Sub { get; }
        public int CommonVariant { get; }
        public PipeArt Pipes { get; }

        public AreaTilesets(Tileset common, int commonVariant, Tileset level, Tileset sub, PipeArt pipes) {
            Common = common;
            CommonVariant = commonVariant;
            Level = level;
            Sub = sub;
            Pipes = pipes;
        }

        public Tileset this[TilesetSlot slot] => slot switch {
            TilesetSlot.Common => Common,
            TilesetSlot.Level => Level,
            _ => Sub,
        };
    }

    /// <summary>Game-level access to a validated ROM: areas, tilesets and expanded grids.</summary>
    public sealed class NsmbGame {
        public const int CommonVariantCount = 4;

        // EU-only area with no information on how it is reached; left out on purpose for now.
        public static readonly IReadOnlyCollection<string> ExcludedAreas = new[] { "F11_3" };

        private const string CoursePrefix = "course/";
        private const string BgdatSuffix = "_bgdat.bin";
        private const string BinSuffix = ".bin";
        private static readonly string[] CommonPaletteNames = {
            "d_2d_A_J_jyotyu_ncl.bin", "d_2d_A_J_jyotyu_B_ncl.bin", "d_2d_A_J_jyotyu_R_ncl.bin", "d_2d_A_J_jyotyu_W_ncl.bin",
        };

        private readonly NsmbRegionProfile profile;
        private readonly byte[] overlay0;
        private readonly Dictionary<string, NitroFile> filesByName = new(StringComparer.Ordinal);
        private readonly Dictionary<int, Tileset> levelTilesets = new();
        private readonly Tileset?[] commonTilesets = new Tileset?[CommonVariantCount];
        private Tileset? subTileset;
        private PipeArt? pipeArt;

        public NdsRom Rom { get; }
        public RomRegion Region { get; }
        public IReadOnlyList<string> AreaNames { get; }

        public NsmbGame(NdsRom rom) {
            RomCheckReport report = RomChecker.Check(rom);
            if (!report.Ok) {
                throw new RomFormatException($"ROM failed validation, refusing to read it ({report.Problems.Count} problems): {report.Problems[0]}");
            }

            Rom = rom;
            Region = RomRegions.FromGameCode(rom.Header.GameCode);
            profile = NsmbRegionProfile.For(Region);
            overlay0 = rom.GetOverlayData(rom.Overlays[0]);

            foreach (KeyValuePair<string, NitroFile> file in rom.FileSystem.ByPath) {
                filesByName[file.Key.Substring(file.Key.LastIndexOf('/') + 1)] = file.Value;
            }

            AreaNames = rom.FileSystem.ByPath.Keys
                .Where(p => p.StartsWith(CoursePrefix, StringComparison.Ordinal) && !p.EndsWith(BgdatSuffix, StringComparison.Ordinal))
                .Select(p => p.Substring(CoursePrefix.Length, p.Length - CoursePrefix.Length - BinSuffix.Length))
                .Where(n => !ExcludedAreas.Contains(n))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
        }

        public LevelArea LoadArea(string name) {
            byte[] level = rom(CoursePrefix + name + BinSuffix);
            byte[] bgdat = rom(CoursePrefix + name + BgdatSuffix);
            return LevelArea.Parse(name, level, bgdat);

            byte[] rom(string path) {
                return Rom.GetFileData(path).ToArray();
            }
        }

        public AreaTilesets GetTilesets(LevelArea area) {
            int variant = GetCommonVariant(area.BackgroundScreen);
            pipeArt ??= new PipeArt(Nsbtx.Parse(ReadPlain(Named("I_dokan.nsbtx"))));
            return new AreaTilesets(GetCommonTileset(variant), variant, GetLevelTileset(area.Settings.Tileset), GetSubTileset(), pipeArt);
        }

        public LevelGrid ExpandArea(LevelArea area, AreaTilesets tilesets) {
            LevelGrid grid = new();
            foreach (LevelObject placed in area.Objects) {
                if (placed.Tileset > (int) TilesetSlot.Sub) {
                    grid.MissingObjects++;
                    continue;
                }

                TilesetSlot slot = (TilesetSlot) placed.Tileset;
                if (slot == TilesetSlot.Common && placed.Id == 0) {
                    Fill(grid, placed, 0);
                    continue;
                }

                ObjectDefinition? definition = tilesets[slot].GetObject(placed.Id);
                if (definition == null || !definition.IsRenderable) {
                    grid.MissingObjects++;
                    continue;
                }
                if (placed.Width == 0 || placed.Height == 0) {
                    continue;
                }

                int[,] block = definition.Render(placed.Width, placed.Height);
                for (int x = 0; x < placed.Width; x++) {
                    for (int y = 0; y < placed.Height; y++) {
                        int cell = block[x, y];
                        if (cell != ObjectDefinition.KeepCell) {
                            grid.Set(placed.X + x, placed.Y + y, (ushort) Math.Max(cell, 0));
                        }
                    }
                }
            }
            return grid;
        }

        public Tileset GetLevelTileset(int id) {
            if (id < 0 || id >= NsmbRegionProfile.TilesetCount) {
                throw new RomFormatException($"Tileset ID {id} is out of range.");
            }
            if (!levelTilesets.TryGetValue(id, out Tileset? tileset)) {
                tileset = new Tileset(
                    TilesetSlot.Level,
                    ReadCompressed(TableFile(Overlay0Table.TilesetGraphics, id)),
                    ReadCompressed(TableFile(Overlay0Table.TilesetPalette, id)),
                    ReadPlain(TableFile(Overlay0Table.TilesetMap16, id)),
                    ReadPlain(TableFile(Overlay0Table.TilesetObjectIndex, id)),
                    ReadPlain(TableFile(Overlay0Table.TilesetObjects, id)),
                    ReadPlain(TableFile(Overlay0Table.TilesetBehaviors, id)));
                levelTilesets[id] = tileset;
            }
            return tileset;
        }

        public Tileset GetCommonTileset(int variant) {
            return commonTilesets[variant] ??= new Tileset(
                TilesetSlot.Common,
                ReadCompressed(Named("d_2d_A_J_jyotyu_ncg.bin")),
                ReadCompressed(Named(CommonPaletteNames[variant])),
                ReadPlain(Named("d_2d_PA_A_J_jyotyu.bin")),
                ReadPlain(Named("A_J_jyotyu_hd.bin")),
                ReadPlain(Named("A_J_jyotyu.bin")),
                overlay0.AsSpan(profile.CommonBehaviorsOffset, NsmbRegionProfile.CommonBehaviorsSize).ToArray());
        }

        public Tileset GetSubTileset() {
            return subTileset ??= new Tileset(
                TilesetSlot.Sub,
                ReadCompressed(Named("d_2d_I_S_tikei_nohara_ncg.bin")),
                ReadCompressed(Named("d_2d_I_S_tikei_nohara_ncl.bin")),
                ReadPlain(Named("d_2d_PA_I_S_nohara.bin")),
                ReadPlain(Named("I_S_nohara_hd.bin")),
                ReadPlain(Named("I_S_nohara.bin")),
                ReadPlain(Named("NoHaRaSubUnitChangeData.bin")));
        }

        private int GetCommonVariant(int backgroundScreen) {
            int offset = profile.CommonPaletteTableOffset + backgroundScreen;
            if (offset >= overlay0.Length) {
                throw new RomFormatException($"Background screen {backgroundScreen} is outside the common palette table.");
            }
            int variant = overlay0[offset];
            return variant < CommonVariantCount ? variant : 0;
        }

        private NitroFile TableFile(Overlay0Table table, int id) {
            return RomChecker.GetTableFile(Rom, overlay0, profile, table, id)
                ?? throw new RomFormatException($"Table {table} has no file for tileset {id}.");
        }

        private NitroFile Named(string name) {
            return filesByName.TryGetValue(name, out NitroFile? file)
                ? file
                : throw new RomFormatException($"ROM has no file named '{name}'.");
        }

        private byte[] ReadPlain(NitroFile file) {
            return Rom.GetFileData(file).ToArray();
        }

        private byte[] ReadCompressed(NitroFile file) {
            return NitroCompression.Decompress(Rom.GetFileData(file).Span);
        }

        private static void Fill(LevelGrid grid, LevelObject placed, ushort cell) {
            for (int x = 0; x < placed.Width; x++) {
                for (int y = 0; y < placed.Height; y++) {
                    grid.Set(placed.X + x, placed.Y + y, cell);
                }
            }
        }
    }
}
