using System;
using System.IO;
using NsmbRom.Nds;
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
        public void UsaRomParses() {
            AssertParses("NSMB_USA.nds", "A2DE");
        }

        [RomFact("NSMB_EU.nds")]
        public void EuRomParses() {
            AssertParses("NSMB_EU.nds", "A2DP");
        }

        [RomFact("NSMB_EU_DUMP.nds")]
        public void EuDumpRomParses() {
            AssertParses("NSMB_EU_DUMP.nds", "A2DP");
        }

        private static void AssertParses(string fileName, string gameCode) {
            NdsHeader header = NdsHeader.Parse(File.ReadAllBytes(RomPaths.Find(fileName)!));

            Assert.Equal(gameCode, header.GameCode);
            Assert.True(header.FileCount > 0);
            Assert.True(header.Arm9OverlayCount > 0);
        }
    }
}
