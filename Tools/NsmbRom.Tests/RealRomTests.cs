using System;
using System.Collections.Generic;
using System.IO;
using NsmbRom.Nds;
using NsmbRom.Nitro;
using NsmbRom.Nsmb;
using Xunit;

namespace NsmbRom.Tests {
    /// <summary>Skipped when the ROM is not in Roms/, so a fresh clone still passes.</summary>
    public sealed class RomFactAttribute : FactAttribute {
        public RomFactAttribute(string fileName) {
            if (RomPaths.Find(fileName) == null) {
                Skip = $"Roms/{fileName} not found";
            }
        }
    }

    internal static class RomPaths {
        public static string? Find(string fileName) {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent) {
                string candidate = Path.Combine(dir.FullName, "Roms", fileName);
                if (File.Exists(candidate)) {
                    return candidate;
                }
            }
            return null;
        }
    }

    public class RealRomTests {
        [RomFact("NSMB_USA.nds")]
        public void UsaRomChecksOut() {
            AssertChecksOut("NSMB_USA.nds", "A2DE", 13);
        }

        [RomFact("NSMB_EU_DUMP.nds")]
        public void EuDumpRomChecksOut() {
            AssertChecksOut("NSMB_EU_DUMP.nds", "A2DP", 13);
        }

        [RomFact("NSMB_EU.nds")]
        public void EditedEuRomIsRefused() {
            RomCheckReport report = RomChecker.Check(LoadRom("NSMB_EU.nds"));

            Assert.False(report.Ok);
            Assert.All(report.Problems, p => Assert.StartsWith("overlay 65", p));
        }

        [RomFact("NSMB_USA.nds")]
        public void AllUsaAreasExpandWithinTheirTilesets() {
            AssertAreasExpand("NSMB_USA.nds", 190);
        }

        [RomFact("NSMB_EU_DUMP.nds")]
        public void AllEuAreasExceptTheExcludedOneExpand() {
            AssertAreasExpand("NSMB_EU_DUMP.nds", 190);
        }

        [RomFact("NSMB_USA.nds")]
        public void PipeArtResolvesPiecesColoursAndInvisibleFillers() {
            NdsRom rom = LoadRom("NSMB_USA.nds");
            Dictionary<int, Nsbtx> hubs = new();
            foreach (KeyValuePair<int, string> hub in PipeArt.HubFiles) {
                hubs[hub.Key] = Nsbtx.Parse(rom.GetFileData("polygon_unit/" + hub.Value).ToArray());
            }
            PipeArt pipes = new(Nsbtx.Parse(rom.GetFileData("polygon_unit/I_dokan.nsbtx").ToArray()), hubs);

            byte[]? green = pipes.GetTile(TilesetSlot.Common, 0x9E, 0x01010004);
            byte[]? greenSolidVariant = pipes.GetTile(TilesetSlot.Common, 0x74, 0x01010024);
            byte[]? red = pipes.GetTile(TilesetSlot.Common, 0xCE, 0x01010084);

            Assert.NotNull(green);
            Assert.Equal(green, greenSolidVariant);
            Assert.NotNull(red);
            Assert.NotEqual(green, red);
            Assert.NotNull(pipes.GetTile(TilesetSlot.Common, 0x7C, 0x0101003F));
            Assert.Null(pipes.GetTile(TilesetSlot.Common, 0x95, 0x0101003F));
            Assert.Null(pipes.GetTile(TilesetSlot.Common, 0x20, 0x00010000));
            Assert.NotEqual(pipes.GetTile(TilesetSlot.Common, 0x78, 0x01010026), pipes.GetTile(TilesetSlot.Common, 0x88, 0x01010026));
        }

        private static void AssertAreasExpand(string fileName, int areas) {
            NsmbGame game = new(LoadRom(fileName));

            Assert.Equal(areas, game.AreaNames.Count);
            Assert.DoesNotContain("F11_3", game.AreaNames);
            foreach (string name in game.AreaNames) {
                LevelArea area = game.LoadArea(name);
                AreaTilesets tilesets = game.GetTilesets(area);
                foreach (ushort cell in game.ExpandArea(area, tilesets).Cells) {
                    TilesetSlot slot = LevelGrid.SplitCell(cell, out int tile);
                    Assert.True(tile < tilesets[slot].TileCount, $"{name}: tile {tile} is outside tileset {slot}");
                }
            }
        }

        private static NdsRom LoadRom(string fileName) {
            return NdsRom.Load(File.ReadAllBytes(RomPaths.Find(fileName)!));
        }

        private static void AssertChecksOut(string fileName, string gameCode, int tables) {
            NdsRom rom = LoadRom(fileName);
            RomCheckReport report = RomChecker.Check(rom);

            Assert.Equal(gameCode, rom.Header.GameCode);
            Assert.Empty(report.Problems);
            Assert.Equal(tables, report.Tables.Count);
            Assert.All(report.Tables, t => Assert.Equal(NsmbRegionProfile.TilesetCount, t.Valid + t.Empty));
        }
    }
}
