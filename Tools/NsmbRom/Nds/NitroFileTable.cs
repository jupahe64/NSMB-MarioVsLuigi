using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace NsmbRom.Nds {
    public sealed class NitroFile {
        public int Id { get; }
        /// <summary>Slash-separated path, or null for files that only exist in the FAT (overlays).</summary>
        public string? Path { get; internal set; }
        public uint Start { get; }
        public uint End { get; }

        public int Size => (int) (End - Start);

        internal NitroFile(int id, uint start, uint end) {
            Id = id;
            Start = start;
            End = end;
        }
    }

    /// <summary>The FNT + FAT pair shared by the ROM file system and NARC archives.</summary>
    public sealed class NitroFileTable {
        private const int FatEntrySize = 8;
        private const int DirEntrySize = 8;
        private const ushort RootDirId = 0xF000;

        public IReadOnlyList<NitroFile> Files { get; }
        public IReadOnlyDictionary<string, NitroFile> ByPath { get; }

        private NitroFileTable(NitroFile[] files, Dictionary<string, NitroFile> byPath) {
            Files = files;
            ByPath = byPath;
        }

        public static NitroFileTable Parse(ReadOnlySpan<byte> fnt, ReadOnlySpan<byte> fat) {
            NitroFile[] files = new NitroFile[fat.Length / FatEntrySize];
            for (int i = 0; i < files.Length; i++) {
                uint start = BinaryPrimitives.ReadUInt32LittleEndian(fat.Slice(i * FatEntrySize));
                uint end = BinaryPrimitives.ReadUInt32LittleEndian(fat.Slice((i * FatEntrySize) + 4));
                if (end < start || end > int.MaxValue) {
                    throw new RomFormatException($"FAT entry {i} has a bad range 0x{start:X}-0x{end:X}.");
                }
                files[i] = new NitroFile(i, start, end);
            }

            Dictionary<string, NitroFile> byPath = new(StringComparer.Ordinal);
            if (fnt.Length >= DirEntrySize) {
                ReadDirectory(fnt, RootDirId, "", files, byPath, new HashSet<int>());
            }
            return new NitroFileTable(files, byPath);
        }

        private static void ReadDirectory(ReadOnlySpan<byte> fnt, int dirId, string prefix, NitroFile[] files, Dictionary<string, NitroFile> byPath, HashSet<int> visited) {
            if ((dirId & 0xF000) != RootDirId || !visited.Add(dirId)) {
                throw new RomFormatException($"FNT has a bad or repeated directory ID 0x{dirId:X4}.");
            }

            int entry = (dirId & 0xFFF) * DirEntrySize;
            if (entry + DirEntrySize > fnt.Length) {
                throw new RomFormatException($"FNT directory 0x{dirId:X4} is out of range.");
            }

            int pos = (int) Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(fnt.Slice(entry)), int.MaxValue);
            int fileId = BinaryPrimitives.ReadUInt16LittleEndian(fnt.Slice(entry + 4));
            while (true) {
                if (pos >= fnt.Length) {
                    throw new RomFormatException($"FNT directory 0x{dirId:X4} runs past the table.");
                }

                byte control = fnt[pos++];
                int nameLength = control & 0x7F;
                if (nameLength == 0) {
                    return;
                }
                if (pos + nameLength > fnt.Length) {
                    throw new RomFormatException($"FNT name in directory 0x{dirId:X4} runs past the table.");
                }

                string name = Encoding.UTF8.GetString(fnt.Slice(pos, nameLength));
                pos += nameLength;
                if (name == "." || name == ".." || name.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0) {
                    throw new RomFormatException($"FNT contains an unsafe name '{name}'.");
                }

                if ((control & 0x80) != 0) {
                    if (pos + 2 > fnt.Length) {
                        throw new RomFormatException($"FNT entry '{name}' runs past the table.");
                    }
                    int subDirId = BinaryPrimitives.ReadUInt16LittleEndian(fnt.Slice(pos));
                    pos += 2;
                    ReadDirectory(fnt, subDirId, prefix + name + "/", files, byPath, visited);
                } else {
                    if (fileId >= files.Length) {
                        throw new RomFormatException($"FNT file '{prefix}{name}' has ID {fileId}, past the FAT ({files.Length} entries).");
                    }
                    if (files[fileId].Path != null) {
                        throw new RomFormatException($"File ID {fileId} is named twice: '{files[fileId].Path}' and '{prefix}{name}'.");
                    }
                    files[fileId].Path = prefix + name;
                    byPath.Add(prefix + name, files[fileId]);
                    fileId++;
                }
            }
        }
    }
}
