using System;
using System.IO;
using NsmbRom.Nds;
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
