using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NsmbRom;
using NsmbRom.Nds;
using NsmbRom.Nsmb;

namespace NsmbExtractor {
    public static class Program {
        private const string Usage = """
            Usage:
              NsmbExtractor info <rom.nds>
              NsmbExtractor check <rom.nds>
              NsmbExtractor dump-fs <rom.nds> [outDir]
              NsmbExtractor dump-overlays <rom.nds> [outDir]
              NsmbExtractor compare-fs <a.nds> <b.nds>
            Default outDir: RomAnalysis/<rom name>/{fs,code}
            """;

        public static int Main(string[] args) {
            return (args.FirstOrDefault(), args.Length) switch {
                ("info", 2) => RunGuarded(() => PrintInfo(args[1])),
                ("check", 2) => RunGuarded(() => RunCheck(args[1]) ? 0 : 1),
                ("dump-fs", 2 or 3) => RunGuarded(() => Dumper.DumpFileSystem(LoadRom(args[1]), OutDir(args, "fs"))),
                ("dump-overlays", 2 or 3) => RunGuarded(() => Dumper.DumpOverlays(LoadRom(args[1]), OutDir(args, "code"))),
                ("compare-fs", 3) => RunGuarded(() => RomComparer.Compare(LoadRom(args[1]), LoadRom(args[2]))),
                _ => PrintUsage(),
            };
        }

        private static int PrintUsage() {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        private static int RunGuarded(Action command) {
            return RunGuarded(() => {
                command();
                return 0;
            });
        }

        private static int RunGuarded(Func<int> command) {
            try {
                return command();
            } catch (Exception e) when (e is RomFormatException or IOException or UnauthorizedAccessException) {
                Console.Error.WriteLine($"error: {e.Message}");
                return 1;
            }
        }

        private static NdsRom LoadRom(string path) {
            return NdsRom.Load(File.ReadAllBytes(path));
        }

        private static string OutDir(string[] args, string kind) {
            return args.Length == 3 ? args[2] : Path.Combine("RomAnalysis", Path.GetFileNameWithoutExtension(args[1]), kind);
        }

        private static void PrintInfo(string path) {
            byte[] image = File.ReadAllBytes(path);
            NdsRom rom = NdsRom.Load(image);
            NdsHeader header = rom.Header;

            Console.WriteLine($"File:          {Path.GetFileName(path)} ({image.Length} bytes)");
            Console.WriteLine($"Title:         {header.Title}");
            Console.WriteLine($"Game code:     {header.GameCode} ({RomRegions.FromGameCode(header.GameCode)})");
            Console.WriteLine($"Maker:         {header.MakerCode}");
            Console.WriteLine($"Version:       {header.RomVersion}");
            Console.WriteLine($"Capacity:      {header.DeviceCapacity} bytes");
            Console.WriteLine($"Used size:     {header.UsedRomSize} bytes");
            Console.WriteLine($"Header CRC:    0x{header.HeaderCrc:X4} (ok)");
            Console.WriteLine($"CRC32:         {Checksums.Crc32(image):X8}");
            Console.WriteLine($"SHA1:          {BitConverter.ToString(SHA1.HashData(image)).Replace("-", "")}");
            Console.WriteLine($"ARM9 entry:    0x{header.Arm9EntryAddress:X8}, RAM 0x{header.Arm9RamAddress:X8}");
            Console.WriteLine($"ARM7 entry:    0x{header.Arm7EntryAddress:X8}, RAM 0x{header.Arm7RamAddress:X8}");
            Console.WriteLine($"Banner offset: 0x{header.BannerOffset:X8}");
            Console.WriteLine($"Files:         {header.FileCount} ({rom.FileSystem.ByPath.Count} named)");
            Console.WriteLine($"Overlays:      {header.Arm9OverlayCount} ARM9, {header.Arm7OverlayCount} ARM7");
            Console.WriteLine("Sections:");
            foreach (RomSection section in header.Sections) {
                Console.WriteLine($"  {section.Name,-20} offset 0x{section.Offset:X8}  size 0x{section.Size:X8}");
            }
        }

        private static bool RunCheck(string path) {
            NdsRom rom = LoadRom(path);
            RomCheckReport report = RomChecker.Check(rom);

            Console.WriteLine($"{Path.GetFileName(path)} ({rom.Header.GameCode})");
            Console.WriteLine($"Files:    {report.NamedFiles} named, {report.UnnamedFiles} unnamed, {report.FilesInUsedRange} inside the used size");
            Console.WriteLine($"Content:  {report.NarcFiles} NARCs, {report.LzFiles} LZ/RLE files");
            Console.WriteLine($"ARM9:     {(report.Arm9WasCompressed ? "compressed" : "plain")}, {report.Arm9UnpackedSize} bytes in RAM");
            Console.WriteLine($"Overlays: {report.OverlaysCompressed} compressed, {report.OverlaysPlain} plain");
            Console.WriteLine($"Levels:   {report.LevelFiles} level files, {report.LevelFilesWithoutBgdat} without bgdat");
            foreach (TableReport table in report.Tables) {
                Console.WriteLine($"Table {table.Table,-24} {table.Valid} valid, {table.Empty} empty, {table.DistinctFiles.Count} distinct files, {table.Compressed} LZ, {table.Grouped} tagged (tags {string.Join("/", table.GroupTags.OrderBy(t => t).Select(t => t.ToString("X2")))}, index {string.Join("/", table.GroupIndices)})");
            }

            foreach (string problem in report.Problems) {
                Console.WriteLine($"PROBLEM: {problem}");
            }
            Console.WriteLine(report.Ok ? "Result: OK" : $"Result: FAILED ({report.Problems.Count} problems)");
            return report.Ok;
        }
    }
}
