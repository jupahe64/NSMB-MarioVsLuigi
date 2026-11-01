using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NsmbRom;
using NsmbRom.Compression;
using NsmbRom.Imaging;
using NsmbRom.Nds;
using NsmbRom.Nitro;

namespace NsmbExtractor {
    /// <summary>Writes raw and decompressed ROM contents for reverse engineering. Output belongs in RomAnalysis/.</summary>
    public static class Dumper {
        public static void DumpFileSystem(NdsRom rom, string outDir) {
            string rawDir = Path.Combine(outDir, "files");
            string decompressedDir = Path.Combine(outDir, "decompressed");
            List<string> listing = new();
            int decompressed = 0;

            foreach (NitroFile file in rom.FileSystem.Files) {
                string relativePath = file.Path ?? $"_unnamed/{file.Id:D4}.bin";
                ReadOnlySpan<byte> data = rom.GetFileData(file).Span;
                WriteFile(rawDir, relativePath, data);

                bool isCompressed = NitroCompression.TryDecompress(data, out byte[]? output);
                if (isCompressed) {
                    WriteFile(decompressedDir, relativePath, output);
                    decompressed++;
                }
                string compression = isCompressed ? $"lz->{output!.Length}" : "-";
                listing.Add($"{file.Id:D4} 0x{file.Start:X8} {file.Size,9} {compression,-14} {relativePath}");
            }

            File.WriteAllLines(Path.Combine(outDir, "files.txt"), listing);
            Console.WriteLine($"Wrote {rom.FileSystem.Files.Count} files to {rawDir}");
            Console.WriteLine($"Wrote {decompressed} decompressed files to {decompressedDir}");
        }

        public static void DumpOverlays(NdsRom rom, string outDir) {
            Directory.CreateDirectory(outDir);
            WriteFile(outDir, "arm9.bin", rom.Arm9Data.Span);
            WriteFile(outDir, "arm9.dec.bin", rom.GetDecompressedArm9());
            WriteFile(outDir, "arm7.bin", rom.Arm7Data.Span);
            WriteFile(outDir, "y9.bin", rom.Arm9OverlayTableData.Span);

            List<string> listing = new();
            foreach (NdsOverlay overlay in rom.Overlays) {
                ReadOnlySpan<byte> stored = rom.GetFileData(rom.FileSystem.Files[overlay.FileId]).Span;
                WriteFile(outDir, $"ov_{overlay.Id:D3}.bin", stored);
                WriteFile(outDir, $"ov_{overlay.Id:D3}.dec.bin", rom.GetOverlayData(overlay));
                listing.Add($"{overlay.Id:D3} file {overlay.FileId:D4} ram 0x{overlay.RamAddress:X8} size 0x{overlay.RamSize:X6} bss 0x{overlay.BssSize:X6} stored 0x{stored.Length:X6} flags 0x{overlay.Flags:X2}");
            }

            File.WriteAllLines(Path.Combine(outDir, "overlays.txt"), listing);
            Console.WriteLine($"Wrote ARM9, ARM7 and {rom.Overlays.Count} overlays to {outDir}");
        }

        /// <summary>Writes every polygon_unit texture once per palette, with a listing of names and sizes.</summary>
        public static void DumpTextures(NdsRom rom, string outDir) {
            const string folder = "polygon_unit/";
            Directory.CreateDirectory(outDir);
            List<string> listing = new();
            foreach (string path in rom.FileSystem.ByPath.Keys.Where(p => p.StartsWith(folder, StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal)) {
                string unit = Path.GetFileNameWithoutExtension(path);
                Nsbtx nsbtx = Nsbtx.Parse(rom.GetFileData(path).ToArray());
                listing.Add($"{unit}: palettes {string.Join(" ", nsbtx.Palettes.Select(p => $"{p.Name}({p.Size / 2})"))}");
                foreach (NsbtxTexture texture in nsbtx.Textures) {
                    listing.Add($"  {texture.Name} {texture.Width}x{texture.Height} format {texture.Format}{(texture.Color0Transparent ? " transparent0" : "")}");
                    foreach (NsbtxPalette palette in nsbtx.Palettes) {
                        byte[]? rgba = nsbtx.Decode(texture, palette);
                        if (rgba == null) {
                            continue;
                        }
                        using FileStream file = File.Create(Path.Combine(outDir, $"{unit}.{texture.Name}.{palette.Name}.png"));
                        PngWriter.Write(file, rgba, texture.Width, texture.Height);
                    }
                }
            }
            File.WriteAllLines(Path.Combine(outDir, "textures.txt"), listing);
            Console.WriteLine($"Wrote textures to {outDir}");
        }

        private static void WriteFile(string root, string relativePath, ReadOnlySpan<byte> data) {
            string fullRoot = Path.GetFullPath(root);
            string path = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                throw new RomFormatException($"Refusing to write outside the output folder: '{relativePath}'.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, data.ToArray());
        }
    }

    /// <summary>File-by-file comparison of two ROMs, by path for named files and by ID for code.</summary>
    public static class RomComparer {
        private const int MaxListed = 500;

        public static void Compare(NdsRom a, NdsRom b) {
            HashSet<string> pathsA = a.FileSystem.ByPath.Keys.ToHashSet(StringComparer.Ordinal);
            HashSet<string> pathsB = b.FileSystem.ByPath.Keys.ToHashSet(StringComparer.Ordinal);
            List<string> onlyA = pathsA.Except(pathsB).OrderBy(p => p, StringComparer.Ordinal).ToList();
            List<string> onlyB = pathsB.Except(pathsA).OrderBy(p => p, StringComparer.Ordinal).ToList();
            List<string> common = pathsA.Intersect(pathsB).OrderBy(p => p, StringComparer.Ordinal).ToList();
            List<string> different = common.Where(p => !a.GetFileData(p).Span.SequenceEqual(b.GetFileData(p).Span)).ToList();

            Console.WriteLine($"Named files: {pathsA.Count} vs {pathsB.Count}, {common.Count} in common, {different.Count} differ");
            PrintList("Only in first", onlyA);
            PrintList("Only in second", onlyB);
            PrintList("Different", different);
            PrintDirectoryCounts("Different by top folder", different);

            Console.WriteLine($"ARM9 identical (decompressed): {a.GetDecompressedArm9().AsSpan().SequenceEqual(b.GetDecompressedArm9())}");
            Console.WriteLine($"ARM7 identical: {a.Arm7Data.Span.SequenceEqual(b.Arm7Data.Span)}");

            int sharedOverlays = Math.Min(a.Overlays.Count, b.Overlays.Count);
            List<string> overlays = Enumerable.Range(0, sharedOverlays)
                .Where(i => !StoredOverlay(a, i).SequenceEqual(StoredOverlay(b, i)))
                .Select(i => i.ToString())
                .ToList();
            Console.WriteLine($"Overlays: {a.Overlays.Count} vs {b.Overlays.Count}, {overlays.Count} of the first {sharedOverlays} differ");
            PrintList("Different overlays", overlays);
        }

        private static ReadOnlySpan<byte> StoredOverlay(NdsRom rom, int id) {
            return rom.GetFileData(rom.FileSystem.Files[rom.Overlays[id].FileId]).Span;
        }

        private static void PrintDirectoryCounts(string title, List<string> paths) {
            if (paths.Count == 0) {
                return;
            }
            Console.WriteLine($"{title}:");
            foreach (IGrouping<string, string> group in paths.GroupBy(p => p.Contains('/') ? p.Substring(0, p.IndexOf('/')) : "(root)").OrderBy(g => g.Key, StringComparer.Ordinal)) {
                Console.WriteLine($"  {group.Key}: {group.Count()}");
            }
        }

        private static void PrintList(string title, List<string> items) {
            if (items.Count == 0) {
                return;
            }
            Console.WriteLine($"{title} ({items.Count}):");
            foreach (string item in items.Take(MaxListed)) {
                Console.WriteLine($"  {item}");
            }
            if (items.Count > MaxListed) {
                Console.WriteLine($"  ... and {items.Count - MaxListed} more");
            }
        }
    }
}
