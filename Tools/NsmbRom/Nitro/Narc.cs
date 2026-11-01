using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using NsmbRom.Nds;

namespace NsmbRom.Nitro {
    /// <summary>A NARC archive: a FAT, an FNT and a data block in chunks, with offsets relative to the data block.</summary>
    public sealed class Narc {
        private const int MinChunkSize = 8;
        private const int FatHeaderSize = 12;

        private readonly byte[] data;
        private readonly int dataStart;

        public NitroFileTable FileTable { get; }

        private Narc(byte[] data, int dataStart, NitroFileTable fileTable) {
            this.data = data;
            this.dataStart = dataStart;
            FileTable = fileTable;
        }

        public static bool HasMagic(ReadOnlySpan<byte> data) {
            return data.Length >= 4 && Encoding.ASCII.GetString(data.Slice(0, 4)) == "NARC";
        }

        public static Narc Parse(byte[] data) {
            if (!HasMagic(data) || data.Length < 0x10) {
                throw new RomFormatException("Not a NARC archive.");
            }

            int headerSize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x0C));
            ReadOnlySpan<byte> fat = default;
            ReadOnlySpan<byte> fnt = default;
            int dataStart = -1;
            int dataEnd = -1;
            int pos = headerSize;
            while (pos + MinChunkSize <= data.Length) {
                string magic = Encoding.ASCII.GetString(data, pos, 4);
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos + 4));
                if (size < MinChunkSize || size > data.Length - pos) {
                    throw new RomFormatException($"NARC chunk '{magic}' at 0x{pos:X} has a bad size 0x{size:X}.");
                }

                ReadOnlySpan<byte> body = data.AsSpan(pos + MinChunkSize, (int) size - MinChunkSize);
                switch (magic) {
                case "BTAF":
                    if (size < FatHeaderSize) {
                        throw new RomFormatException("NARC file table is too small.");
                    }
                    int count = BinaryPrimitives.ReadUInt16LittleEndian(body);
                    if ((long) count * 8 > body.Length - 4) {
                        throw new RomFormatException($"NARC file table is too small for {count} files.");
                    }
                    fat = body.Slice(4, count * 8);
                    break;
                case "BTNF":
                    fnt = body;
                    break;
                case "GMIF":
                    dataStart = pos + MinChunkSize;
                    dataEnd = pos + (int) size;
                    break;
                default:
                    throw new RomFormatException($"Unknown NARC chunk '{magic}'.");
                }
                pos += (int) size;
            }

            if (dataStart < 0) {
                throw new RomFormatException("NARC has no data chunk.");
            }

            NitroFileTable table = NitroFileTable.Parse(fnt, fat);
            foreach (NitroFile file in table.Files) {
                if (file.End > dataEnd - dataStart) {
                    throw new RomFormatException($"NARC file {file.Id} ends past the data chunk.");
                }
            }
            return new Narc(data, dataStart, table);
        }

        public ReadOnlyMemory<byte> GetFileData(NitroFile file) {
            return data.AsMemory(dataStart + (int) file.Start, file.Size);
        }
    }
}
