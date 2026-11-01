using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using NsmbRom.Compression;

namespace NsmbRom.Nds {
    /// <summary>A parsed ROM image: header, file system and overlays. Parsing refuses inconsistent images.</summary>
    public sealed class NdsRom {
        private const uint NitroCode = 0xDEC00621;
        private const int Arm9FooterSize = 12;
        private const int ModuleParamsCompressedEnd = 0x14;
        private const int ModuleParamsNitroCode = 0x1C;

        private readonly byte[] image;

        public NdsHeader Header { get; }
        public NitroFileTable FileSystem { get; }
        public IReadOnlyList<NdsOverlay> Overlays { get; }

        public ReadOnlyMemory<byte> Arm7Data => Section(Header.Arm7);
        public ReadOnlyMemory<byte> Arm9OverlayTableData => Section(Header.Arm9OverlayTable);
        public ReadOnlyMemory<byte> Arm9Data => Section(Header.Arm9);

        private NdsRom(byte[] image) {
            this.image = image;
            Header = NdsHeader.Parse(image);
            FileSystem = NitroFileTable.Parse(Section(Header.FileNameTable).Span, Section(Header.FileAllocationTable).Span);
            CheckFileRanges();
            Overlays = ReadOverlays();
        }

        public static NdsRom Load(byte[] image) {
            return new NdsRom(image);
        }

        public ReadOnlyMemory<byte> GetFileData(NitroFile file) {
            return image.AsMemory((int) file.Start, file.Size);
        }

        public ReadOnlyMemory<byte> GetFileData(string path) {
            if (!FileSystem.ByPath.TryGetValue(path, out NitroFile? file)) {
                throw new RomFormatException($"ROM has no file '{path}'.");
            }
            return GetFileData(file);
        }

        /// <summary>The overlay as it sits in RAM after loading, checked against the size in the overlay table.</summary>
        public byte[] GetOverlayData(NdsOverlay overlay) {
            ReadOnlySpan<byte> stored = GetFileData(FileSystem.Files[overlay.FileId]).Span;
            byte[] data = overlay.IsCompressed ? BackwardLz.Decompress(stored) : stored.ToArray();
            if (data.Length != overlay.RamSize) {
                throw new RomFormatException($"Overlay {overlay.Id} is 0x{data.Length:X} bytes, the table says 0x{overlay.RamSize:X}.");
            }
            return data;
        }

        /// <summary>RAM offset where the BLZ-compressed part of the ARM9 binary ends; 0 when it is stored plain or unknown.</summary>
        public uint GetArm9CompressedEnd() {
            ReadOnlySpan<byte> arm9 = Arm9Data.Span;
            int footer = (int) Header.Arm9.End;
            if (footer + Arm9FooterSize > image.Length || BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(footer)) != NitroCode) {
                return 0;
            }

            uint paramsOffset = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(footer + 4));
            if (paramsOffset + ModuleParamsNitroCode + 4 > arm9.Length) {
                return 0;
            }

            int offset = (int) paramsOffset;
            if (BinaryPrimitives.ReadUInt32LittleEndian(arm9.Slice(offset + ModuleParamsNitroCode)) != NitroCode) {
                return 0;
            }
            return BinaryPrimitives.ReadUInt32LittleEndian(arm9.Slice(offset + ModuleParamsCompressedEnd));
        }

        /// <summary>The ARM9 binary as it sits in RAM after its startup code has unpacked it.</summary>
        public byte[] GetDecompressedArm9() {
            uint compressedEnd = GetArm9CompressedEnd();
            ReadOnlySpan<byte> arm9 = Arm9Data.Span;
            if (compressedEnd == 0) {
                return arm9.ToArray();
            }

            long size = (long) compressedEnd - Header.Arm9RamAddress;
            if (size <= 0 || size > arm9.Length) {
                throw new RomFormatException($"ARM9 compressed end 0x{compressedEnd:X8} is outside the binary.");
            }

            byte[] unpacked = BackwardLz.Decompress(arm9.Slice(0, (int) size));
            byte[] result = new byte[unpacked.Length + arm9.Length - size];
            unpacked.CopyTo(result, 0);
            arm9.Slice((int) size).CopyTo(result.AsSpan(unpacked.Length));
            return result;
        }

        private ReadOnlyMemory<byte> Section(RomSection section) {
            return image.AsMemory((int) section.Offset, (int) section.Size);
        }

        private void CheckFileRanges() {
            ulong previousEnd = 0;
            NitroFile? previous = null;
            foreach (NitroFile file in FileSystem.Files.Where(f => f.Size > 0).OrderBy(f => f.Start)) {
                if (file.End > image.Length) {
                    throw new RomFormatException($"File {file.Id} ends at 0x{file.End:X}, past the end of the ROM.");
                }
                if (file.Start < previousEnd) {
                    throw new RomFormatException($"Files {previous!.Id} and {file.Id} overlap at 0x{file.Start:X}.");
                }
                previousEnd = file.End;
                previous = file;
            }
        }

        private NdsOverlay[] ReadOverlays() {
            ReadOnlySpan<byte> table = Arm9OverlayTableData.Span;
            NdsOverlay[] overlays = new NdsOverlay[Header.Arm9OverlayCount];
            for (int i = 0; i < overlays.Length; i++) {
                NdsOverlay overlay = new(table.Slice(i * NdsOverlay.EntrySize, NdsOverlay.EntrySize));
                if (overlay.Id != i) {
                    throw new RomFormatException($"Overlay table entry {i} has ID {overlay.Id}.");
                }
                if (overlay.FileId >= FileSystem.Files.Count) {
                    throw new RomFormatException($"Overlay {i} refers to file {overlay.FileId}, past the FAT.");
                }
                overlays[i] = overlay;
            }
            return overlays;
        }
    }
}
