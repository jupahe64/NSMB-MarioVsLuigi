using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace NsmbRom.Nds {
    /// <summary>A section of the ROM described by an (offset, size) pair in the header.</summary>
    public readonly struct RomSection {
        public readonly string Name;
        public readonly uint Offset;
        public readonly uint Size;

        public RomSection(string name, uint offset, uint size) {
            Name = name;
            Offset = offset;
            Size = size;
        }

        public ulong End => (ulong) Offset + Size;
    }

    public sealed class NdsHeader {
        public const int HeaderSize = 0x200;
        private const int CrcCoveredBytes = 0x15E;
        private const int FatEntrySize = 8;
        private const int OverlayEntrySize = 32;

        public string Title { get; }
        public string GameCode { get; }
        public string MakerCode { get; }
        public byte RomVersion { get; }
        public long DeviceCapacity { get; }
        public uint UsedRomSize { get; }
        public ushort HeaderCrc { get; }

        public uint Arm9EntryAddress { get; }
        public uint Arm9RamAddress { get; }
        public uint Arm7EntryAddress { get; }
        public uint Arm7RamAddress { get; }
        public uint BannerOffset { get; }

        public RomSection Arm9 { get; }
        public RomSection Arm7 { get; }
        public RomSection FileNameTable { get; }
        public RomSection FileAllocationTable { get; }
        public RomSection Arm9OverlayTable { get; }
        public RomSection Arm7OverlayTable { get; }

        public int FileCount => (int) (FileAllocationTable.Size / FatEntrySize);
        public int Arm9OverlayCount => (int) (Arm9OverlayTable.Size / OverlayEntrySize);
        public int Arm7OverlayCount => (int) (Arm7OverlayTable.Size / OverlayEntrySize);

        public RomSection[] Sections => new[] { Arm9, Arm7, FileNameTable, FileAllocationTable, Arm9OverlayTable, Arm7OverlayTable };

        private NdsHeader(ReadOnlySpan<byte> data) {
            Title = ReadAscii(data.Slice(0x00, 12));
            GameCode = ReadAscii(data.Slice(0x0C, 4));
            MakerCode = ReadAscii(data.Slice(0x10, 2));
            DeviceCapacity = 0x20000L << Math.Min((int) data[0x14], 24);
            RomVersion = data[0x1E];

            Arm9 = new RomSection("ARM9", ReadU32(data, 0x20), ReadU32(data, 0x2C));
            Arm9EntryAddress = ReadU32(data, 0x24);
            Arm9RamAddress = ReadU32(data, 0x28);
            Arm7 = new RomSection("ARM7", ReadU32(data, 0x30), ReadU32(data, 0x3C));
            Arm7EntryAddress = ReadU32(data, 0x34);
            Arm7RamAddress = ReadU32(data, 0x38);
            FileNameTable = new RomSection("FNT", ReadU32(data, 0x40), ReadU32(data, 0x44));
            FileAllocationTable = new RomSection("FAT", ReadU32(data, 0x48), ReadU32(data, 0x4C));
            Arm9OverlayTable = new RomSection("ARM9 overlay table", ReadU32(data, 0x50), ReadU32(data, 0x54));
            Arm7OverlayTable = new RomSection("ARM7 overlay table", ReadU32(data, 0x58), ReadU32(data, 0x5C));
            BannerOffset = ReadU32(data, 0x68);
            UsedRomSize = ReadU32(data, 0x80);
            HeaderCrc = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(CrcCoveredBytes, 2));
        }

        /// <summary>Parses and validates the header of a whole ROM image.</summary>
        /// <exception cref="RomFormatException">The image is not a supported, consistent NDS ROM.</exception>
        public static NdsHeader Parse(ReadOnlySpan<byte> rom) {
            if (rom.Length < HeaderSize) {
                throw new RomFormatException($"File is too small to be an NDS ROM ({rom.Length} bytes).");
            }

            NdsHeader header = new(rom);
            ushort actualCrc = Checksums.Crc16(rom.Slice(0, CrcCoveredBytes));
            if (actualCrc != header.HeaderCrc) {
                throw new RomFormatException($"Header CRC mismatch: stored 0x{header.HeaderCrc:X4}, computed 0x{actualCrc:X4}.");
            }

            RomRegions.FromGameCode(header.GameCode);

            List<string> problems = header.FindLayoutProblems(rom.Length);
            if (problems.Count > 0) {
                throw new RomFormatException("Inconsistent ROM layout: " + string.Join("; ", problems));
            }
            return header;
        }

        private List<string> FindLayoutProblems(long romLength) {
            List<string> problems = new();
            foreach (RomSection section in Sections) {
                if (section.End > (ulong) romLength) {
                    problems.Add($"{section.Name} ends at 0x{section.End:X}, past the end of the file (0x{romLength:X})");
                } else if (section.Size > 0 && section.Offset < HeaderSize) {
                    problems.Add($"{section.Name} starts inside the header (0x{section.Offset:X})");
                }
            }

            if (FileAllocationTable.Size % FatEntrySize != 0) {
                problems.Add($"FAT size 0x{FileAllocationTable.Size:X} is not a multiple of {FatEntrySize}");
            }
            if (Arm9OverlayTable.Size % OverlayEntrySize != 0) {
                problems.Add($"ARM9 overlay table size 0x{Arm9OverlayTable.Size:X} is not a multiple of {OverlayEntrySize}");
            }
            if (Arm7OverlayTable.Size % OverlayEntrySize != 0) {
                problems.Add($"ARM7 overlay table size 0x{Arm7OverlayTable.Size:X} is not a multiple of {OverlayEntrySize}");
            }
            if (UsedRomSize > romLength) {
                problems.Add($"used ROM size 0x{UsedRomSize:X} is larger than the file (0x{romLength:X})");
            }
            return problems;
        }

        private static uint ReadU32(ReadOnlySpan<byte> data, int offset) {
            return BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
        }

        private static string ReadAscii(ReadOnlySpan<byte> data) {
            return Encoding.ASCII.GetString(data).TrimEnd('\0');
        }
    }
}
