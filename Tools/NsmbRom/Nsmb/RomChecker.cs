using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using NsmbRom.Compression;
using NsmbRom.Nds;
using NsmbRom.Nitro;

namespace NsmbRom.Nsmb {
    public sealed class TableReport {
        public Overlay0Table Table { get; }
        public int Valid { get; internal set; }
        public int Compressed { get; internal set; }
        public int NameMismatches { get; internal set; }
        public int BadEntries { get; internal set; }
        public int BadCompression { get; internal set; }
        public int Empty { get; internal set; }
        public int Grouped { get; internal set; }
        /// <summary>High bytes of tagged entries. The low 16 bits are still a plain file ID; the meaning of the tag is unconfirmed.</summary>
        public HashSet<uint> GroupTags { get; } = new();
        public HashSet<int> GroupIndices { get; } = new();
        public HashSet<int> DistinctFiles { get; } = new();
        public string? FirstMismatch { get; internal set; }

        internal TableReport(Overlay0Table table) {
            Table = table;
        }
    }

    public sealed class RomCheckReport {
        public List<string> Problems { get; } = new();
        public List<TableReport> Tables { get; } = new();
        public int OverlaysCompressed { get; internal set; }
        public int OverlaysPlain { get; internal set; }
        public long Arm9UnpackedSize { get; internal set; }
        public bool Arm9WasCompressed { get; internal set; }
        public int NamedFiles { get; internal set; }
        public int UnnamedFiles { get; internal set; }
        public int LevelFiles { get; internal set; }
        public int LevelFilesWithoutBgdat { get; internal set; }
        public int NarcFiles { get; internal set; }
        public int LzFiles { get; internal set; }
        public int FilesInUsedRange { get; internal set; }

        public bool Ok => Problems.Count == 0;
    }

    /// <summary>Checks the format assumptions against a real ROM. Any problem means the tool must not extract from it.</summary>
    public static class RomChecker {
        private const string LevelDirectory = "course/";
        private const string BgdatSuffix = "_bgdat.bin";

        // Substrings every file of a table is expected to carry in its name.
        private static readonly Dictionary<Overlay0Table, string> ExpectedNames = new() {
            [Overlay0Table.TilesetObjectIndex] = "_hd.bin",
            [Overlay0Table.TilesetObjects] = ".bin",
            [Overlay0Table.TilesetBehaviors] = "UnitChangeData",
            [Overlay0Table.TilesetAnimatedGraphics] = "ncg",
            [Overlay0Table.BackgroundGraphics] = "ncg",
            [Overlay0Table.TilesetGraphics] = "ncg",
            [Overlay0Table.ForegroundGraphics] = "ncg",
            [Overlay0Table.ForegroundScreen] = "nsc",
            [Overlay0Table.BackgroundScreen] = "nsc",
            [Overlay0Table.BackgroundPalette] = "ncl",
            [Overlay0Table.TilesetPalette] = "ncl",
            [Overlay0Table.ForegroundPalette] = "ncl",
            [Overlay0Table.TilesetMap16] = "d_2d_PA",
        };

        public static RomCheckReport Check(NdsRom rom) {
            RomCheckReport report = new();
            CheckFiles(rom, report);
            CheckArm9(rom, report);
            byte[]? overlay0 = CheckOverlays(rom, report);
            CheckLevels(rom, report);
            if (overlay0 != null) {
                CheckTables(rom, overlay0, report);
            }
            return report;
        }

        /// <summary>Resolves a table entry to its file; null for the "no file" value.</summary>
        internal static NitroFile? GetTableFile(NdsRom rom, byte[] overlay0, NsmbRegionProfile profile, Overlay0Table table, int id) {
            uint entry = BinaryPrimitives.ReadUInt32LittleEndian(overlay0.AsSpan(profile.TableOffsets[table] + (id * 4)));
            int fileId = (int) (entry & 0xFFFF) + profile.FileIdBase;
            return fileId < rom.FileSystem.Files.Count ? rom.FileSystem.Files[fileId] : null;
        }

        private static void CheckFiles(NdsRom rom, RomCheckReport report) {
            foreach (NitroFile file in rom.FileSystem.Files) {
                if (file.Path == null) {
                    report.UnnamedFiles++;
                } else {
                    report.NamedFiles++;
                }

                if (file.End <= rom.Header.UsedRomSize) {
                    report.FilesInUsedRange++;
                } else {
                    report.Problems.Add($"file {file.Id} ({file.Path}) ends past the used ROM size");
                }

                ReadOnlySpan<byte> data = rom.GetFileData(file).Span;
                if (Narc.HasMagic(data)) {
                    report.NarcFiles++;
                    try {
                        Narc.Parse(data.ToArray());
                    } catch (RomFormatException e) {
                        report.Problems.Add($"NARC {file.Path}: {e.Message}");
                    }
                } else if (NitroCompression.TryDecompress(data, out _)) {
                    report.LzFiles++;
                }
            }

            if (rom.Overlays.Any(o => rom.FileSystem.Files[o.FileId].Path != null)) {
                report.Problems.Add("an overlay refers to a named file");
            }
            if (report.UnnamedFiles != rom.Overlays.Count) {
                report.Problems.Add($"{report.UnnamedFiles} unnamed files, but {rom.Overlays.Count} overlays");
            }
        }

        private static void CheckArm9(NdsRom rom, RomCheckReport report) {
            try {
                report.Arm9WasCompressed = rom.GetArm9CompressedEnd() != 0;
                report.Arm9UnpackedSize = rom.GetDecompressedArm9().Length;
            } catch (RomFormatException e) {
                report.Problems.Add($"ARM9: {e.Message}");
            }
        }

        private static byte[]? CheckOverlays(NdsRom rom, RomCheckReport report) {
            byte[]? overlay0 = null;
            foreach (NdsOverlay overlay in rom.Overlays) {
                if (overlay.IsCompressed) {
                    report.OverlaysCompressed++;
                } else {
                    report.OverlaysPlain++;
                }

                int storedSize = rom.FileSystem.Files[overlay.FileId].Size;
                if (overlay.IsCompressed && overlay.StoredSize != storedSize) {
                    report.Problems.Add($"overlay {overlay.Id}: table says 0x{overlay.StoredSize:X} stored bytes, the file has 0x{storedSize:X}");
                }

                try {
                    byte[] data = rom.GetOverlayData(overlay);
                    if (overlay.Id == 0) {
                        overlay0 = data;
                    }
                } catch (RomFormatException e) {
                    report.Problems.Add($"overlay {overlay.Id}: {e.Message}");
                }
            }

            if (overlay0 == null) {
                report.Problems.Add("overlay 0 is unavailable, cannot check the tables");
            }
            return overlay0;
        }

        private static void CheckLevels(NdsRom rom, RomCheckReport report) {
            foreach (string path in rom.FileSystem.ByPath.Keys) {
                if (!path.StartsWith(LevelDirectory, StringComparison.Ordinal) || path.EndsWith(BgdatSuffix, StringComparison.Ordinal)) {
                    continue;
                }
                report.LevelFiles++;
                string bgdat = path.Substring(0, path.Length - ".bin".Length) + BgdatSuffix;
                if (!rom.FileSystem.ByPath.ContainsKey(bgdat)) {
                    report.LevelFilesWithoutBgdat++;
                }
            }
            if (report.LevelFiles == 0) {
                report.Problems.Add($"no level files in {LevelDirectory}");
            }
        }

        private static void CheckTables(NdsRom rom, byte[] overlay0, RomCheckReport report) {
            RomRegion region = RomRegions.FromGameCode(rom.Header.GameCode);
            NsmbRegionProfile profile = NsmbRegionProfile.For(region);
            if (profile.FileIdBase != rom.Overlays.Count) {
                report.Problems.Add($"profile expects {profile.FileIdBase} overlays, the ROM has {rom.Overlays.Count}");
            }

            foreach (KeyValuePair<Overlay0Table, int> table in profile.TableOffsets) {
                report.Tables.Add(CheckTable(rom, overlay0, profile, table.Key, table.Value));
            }

            foreach (TableReport table in report.Tables) {
                if (table.BadEntries > 0 || table.BadCompression > 0 || table.NameMismatches > 0) {
                    report.Problems.Add($"table {table.Table}: {table.BadEntries} bad entries, {table.BadCompression} failed to decompress, {table.NameMismatches} unexpected names (first: {table.FirstMismatch})");
                }
            }

            if (profile.CommonBehaviorsOffset + NsmbRegionProfile.CommonBehaviorsSize > overlay0.Length
                || profile.SpriteModifiersOffset + (NsmbRegionProfile.SpriteCount * 2) > overlay0.Length) {
                report.Problems.Add("inline tables are past the end of overlay 0");
            }
        }

        private static TableReport CheckTable(NdsRom rom, byte[] overlay0, NsmbRegionProfile profile, Overlay0Table table, int offset) {
            TableReport result = new(table);
            string expected = ExpectedNames[table];
            for (int i = 0; i < NsmbRegionProfile.TilesetCount; i++) {
                int entryOffset = offset + (i * 4);
                if (entryOffset + 4 > overlay0.Length) {
                    result.BadEntries++;
                    continue;
                }

                uint entry = BinaryPrimitives.ReadUInt32LittleEndian(overlay0.AsSpan(entryOffset));
                int fileId = (int) (entry & 0xFFFF) + profile.FileIdBase;
                uint tag = entry >> 24;
                if (entry >> 16 == 0 && fileId == rom.FileSystem.Files.Count) {
                    result.Empty++;
                    continue;
                }
                if ((tag == 0 && (entry >> 16) != 0) || fileId >= rom.FileSystem.Files.Count) {
                    result.BadEntries++;
                    result.FirstMismatch ??= $"entry {i} = 0x{entry:X8}";
                    continue;
                }
                if (tag != 0) {
                    result.Grouped++;
                    result.GroupTags.Add(tag);
                    result.GroupIndices.Add((int) ((entry >> 16) & 0xFF));
                }

                NitroFile file = rom.FileSystem.Files[fileId];
                result.DistinctFiles.Add(fileId);
                if (file.Path == null || !file.Path.Contains(expected)) {
                    result.NameMismatches++;
                    result.FirstMismatch ??= $"entry {i} = {file.Path}";
                }

                ReadOnlySpan<byte> data = rom.GetFileData(file).Span;
                if (data.Length > 0 && data[0] == NitroCompression.Lz77Type && expected != ".bin" && expected != "_hd.bin" && expected != "UnitChangeData") {
                    if (NitroCompression.TryDecompress(data, out _)) {
                        result.Compressed++;
                    } else {
                        result.BadCompression++;
                        result.FirstMismatch ??= $"entry {i} = {file.Path} does not decompress";
                    }
                }
                result.Valid++;
            }
            return result;
        }
    }
}
