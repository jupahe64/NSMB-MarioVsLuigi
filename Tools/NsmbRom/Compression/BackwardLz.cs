using System;
using System.Buffers.Binary;

namespace NsmbRom.Compression {
    /// <summary>
    /// The backward LZ used for overlays and the ARM9 binary. The stream is read from the end of the
    /// data and decoded towards the start, in place, so bytes before the compressed region are kept as is.
    /// </summary>
    public static class BackwardLz {
        private const int FooterSize = 8;
        private const int MinMatch = 3;
        private const int MinDistance = 3;

        public static byte[] Decompress(ReadOnlySpan<byte> data) {
            if (data.Length < FooterSize) {
                throw new RomFormatException("Data is too short for a backward LZ footer.");
            }

            uint sizes = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(data.Length - 8));
            uint growth = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(data.Length - 4));
            int regionSize = (int) (sizes & 0xFFFFFF);
            int headerSize = (int) (sizes >> 24);
            if (headerSize < FooterSize || headerSize > regionSize || regionSize > data.Length) {
                throw new RomFormatException($"Bad backward LZ footer: region 0x{regionSize:X}, header 0x{headerSize:X}, data 0x{data.Length:X}.");
            }
            if (growth > int.MaxValue - (uint) data.Length) {
                throw new RomFormatException($"Bad backward LZ growth 0x{growth:X}.");
            }

            byte[] buffer = new byte[data.Length + growth];
            data.CopyTo(buffer);

            int stop = data.Length - regionSize;
            int src = data.Length - headerSize;
            int dst = buffer.Length;
            while (src > stop) {
                byte flags = buffer[--src];
                for (int bit = 7; bit >= 0 && src > stop; bit--) {
                    if ((flags & (1 << bit)) == 0) {
                        // The output may catch up with the input at the very end: the copy is then a no-op.
                        Require(dst >= src && dst > stop, dst);
                        buffer[--dst] = buffer[--src];
                        continue;
                    }

                    Require(src - stop >= 2, dst);
                    int value = (buffer[src - 1] << 8) | buffer[src - 2];
                    src -= 2;
                    int length = (value >> 12) + MinMatch;
                    int distance = (value & 0xFFF) + MinDistance;
                    Require(dst - length >= stop && dst - 1 + distance < buffer.Length, dst);
                    for (int i = 0; i < length; i++) {
                        dst--;
                        buffer[dst] = buffer[dst + distance];
                    }
                }
            }

            if (dst != stop) {
                throw new RomFormatException($"Backward LZ ended at 0x{dst:X} instead of 0x{stop:X}.");
            }
            return buffer;
        }

        private static void Require(bool condition, int dst) {
            if (!condition) {
                throw new RomFormatException($"Corrupt backward LZ stream at output offset 0x{dst:X}.");
            }
        }
    }
}
