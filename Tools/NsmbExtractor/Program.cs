using System;
using System.IO;
using System.Security.Cryptography;
using NsmbRom;
using NsmbRom.Nds;

namespace NsmbExtractor {
    public static class Program {
        private const string Usage = "Usage: NsmbExtractor info <rom.nds>";

        public static int Main(string[] args) {
            if (args.Length == 2 && args[0] == "info") {
                return RunGuarded(() => PrintInfo(args[1]));
            }
            Console.Error.WriteLine(Usage);
            return 2;
        }

        private static int RunGuarded(Action command) {
            try {
                command();
                return 0;
            } catch (Exception e) when (e is RomFormatException or IOException or UnauthorizedAccessException) {
                Console.Error.WriteLine($"error: {e.Message}");
                return 1;
            }
        }

        private static void PrintInfo(string path) {
            byte[] rom = File.ReadAllBytes(path);
            NdsHeader header = NdsHeader.Parse(rom);

            Console.WriteLine($"File:          {Path.GetFileName(path)} ({rom.Length} bytes)");
            Console.WriteLine($"Title:         {header.Title}");
            Console.WriteLine($"Game code:     {header.GameCode} ({RomRegions.FromGameCode(header.GameCode)})");
            Console.WriteLine($"Maker:         {header.MakerCode}");
            Console.WriteLine($"Version:       {header.RomVersion}");
            Console.WriteLine($"Capacity:      {header.DeviceCapacity} bytes");
            Console.WriteLine($"Used size:     {header.UsedRomSize} bytes");
            Console.WriteLine($"Header CRC:    0x{header.HeaderCrc:X4} (ok)");
            Console.WriteLine($"CRC32:         {Checksums.Crc32(rom):X8}");
            Console.WriteLine($"SHA1:          {BitConverter.ToString(SHA1.HashData(rom)).Replace("-", "")}");
            Console.WriteLine($"ARM9 entry:    0x{header.Arm9EntryAddress:X8}, RAM 0x{header.Arm9RamAddress:X8}");
            Console.WriteLine($"ARM7 entry:    0x{header.Arm7EntryAddress:X8}, RAM 0x{header.Arm7RamAddress:X8}");
            Console.WriteLine($"Banner offset: 0x{header.BannerOffset:X8}");
            Console.WriteLine($"Files:         {header.FileCount}");
            Console.WriteLine($"Overlays:      {header.Arm9OverlayCount} ARM9, {header.Arm7OverlayCount} ARM7");
            Console.WriteLine("Sections:");
            foreach (RomSection section in header.Sections) {
                Console.WriteLine($"  {section.Name,-20} offset 0x{section.Offset:X8}  size 0x{section.Size:X8}");
            }
        }
    }
}
