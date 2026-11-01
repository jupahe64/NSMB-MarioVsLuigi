using System;
using System.Buffers.Binary;

namespace NsmbRom.Nds {
    public sealed class NdsOverlay {
        public const int EntrySize = 32;

        public uint Id { get; }
        public uint RamAddress { get; }
        public uint RamSize { get; }
        public uint BssSize { get; }
        public uint StaticInitStart { get; }
        public uint StaticInitEnd { get; }
        public int FileId { get; }
        /// <summary>Size of the stored file as recorded in the table.</summary>
        public uint StoredSize { get; }
        public byte Flags { get; }

        public bool IsCompressed => (Flags & 1) != 0;

        internal NdsOverlay(ReadOnlySpan<byte> entry) {
            Id = BinaryPrimitives.ReadUInt32LittleEndian(entry);
            RamAddress = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(0x04));
            RamSize = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(0x08));
            BssSize = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(0x0C));
            StaticInitStart = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(0x10));
            StaticInitEnd = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(0x14));
            FileId = (int) Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(0x18)), int.MaxValue);
            uint packed = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(0x1C));
            StoredSize = packed & 0xFFFFFF;
            Flags = (byte) (packed >> 24);
        }
    }
}
