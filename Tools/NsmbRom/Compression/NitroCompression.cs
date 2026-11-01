using System;
using System.Buffers.Binary;

namespace NsmbRom.Compression {
    /// <summary>The header-prefixed BIOS formats: LZ77 (0x10), LZ11 (0x11) and RLE (0x30).</summary>
    public static class NitroCompression {
        public const byte Lz77Type = 0x10;
        public const byte Lz11Type = 0x11;
        public const byte RleType = 0x30;

        // Files are padded to their alignment after the stream ends.
        private const int MaxTrailingPadding = 0x20;

        public static byte[] Decompress(ReadOnlySpan<byte> data) {
            return Decompress(data, out _);
        }

        public static byte[] Decompress(ReadOnlySpan<byte> data, out int consumed) {
            if (data.Length < 4) {
                throw new RomFormatException("Compressed stream is too short for a header.");
            }

            int headerSize = 4;
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(data) >> 8;
            if (size == 0) {
                if (data.Length < 8) {
                    throw new RomFormatException("Compressed stream is too short for an extended header.");
                }
                size = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4));
                headerSize = 8;
            }
            if (size > int.MaxValue) {
                throw new RomFormatException($"Decompressed size 0x{size:X} is too large.");
            }

            byte[] output = new byte[size];
            ReadOnlySpan<byte> body = data.Slice(headerSize);
            int bodyConsumed = data[0] switch {
                Lz77Type => DecodeLz(body, output, extended: false),
                Lz11Type => DecodeLz(body, output, extended: true),
                RleType => DecodeRle(body, output),
                _ => throw new RomFormatException($"Unknown compression type 0x{data[0]:X2}."),
            };
            consumed = headerSize + bodyConsumed;
            return output;
        }

        /// <summary>Strict attempt for data of unknown type: the stream must decode and fill the file.</summary>
        public static bool TryDecompress(ReadOnlySpan<byte> data, out byte[]? output) {
            output = null;
            if (data.Length < 4 || data[0] is not (Lz77Type or Lz11Type or RleType)) {
                return false;
            }
            // Stops a stray type byte from allocating a huge buffer: the best case for these formats is about 1:150.
            if ((BinaryPrimitives.ReadUInt32LittleEndian(data) >> 8) > (long) data.Length * 256) {
                return false;
            }

            try {
                byte[] decoded = Decompress(data, out int consumed);
                if (decoded.Length == 0 || data.Length - consumed >= MaxTrailingPadding) {
                    return false;
                }
                output = decoded;
                return true;
            } catch (RomFormatException) {
                return false;
            }
        }

        private static int DecodeLz(ReadOnlySpan<byte> input, byte[] output, bool extended) {
            int src = 0;
            int dst = 0;
            while (dst < output.Length) {
                byte flags = ReadByte(input, ref src);
                for (int bit = 7; bit >= 0 && dst < output.Length; bit--) {
                    if ((flags & (1 << bit)) == 0) {
                        output[dst++] = ReadByte(input, ref src);
                        continue;
                    }

                    int first = ReadByte(input, ref src);
                    int length;
                    int high;
                    if (extended) {
                        length = ReadLz11Length(input, ref src, first, out high);
                    } else {
                        length = (first >> 4) + 3;
                        high = first & 0xF;
                    }

                    int distance = ((high << 8) | ReadByte(input, ref src)) + 1;
                    if (distance > dst) {
                        throw new RomFormatException($"LZ back-reference of {distance} bytes at output offset {dst}.");
                    }
                    if (length > output.Length - dst) {
                        throw new RomFormatException($"LZ run of {length} bytes overflows the output at offset {dst}.");
                    }
                    for (int i = 0; i < length; i++, dst++) {
                        output[dst] = output[dst - distance];
                    }
                }
            }
            return src;
        }

        private static int ReadLz11Length(ReadOnlySpan<byte> input, ref int src, int first, out int high) {
            int selector = first >> 4;
            if (selector == 0) {
                int next = ReadByte(input, ref src);
                high = next & 0xF;
                return (((first & 0xF) << 4) | (next >> 4)) + 0x11;
            }
            if (selector == 1) {
                int middle = ReadByte(input, ref src);
                int next = ReadByte(input, ref src);
                high = next & 0xF;
                return (((first & 0xF) << 12) | (middle << 4) | (next >> 4)) + 0x111;
            }
            high = first & 0xF;
            return selector + 1;
        }

        private static int DecodeRle(ReadOnlySpan<byte> input, byte[] output) {
            int src = 0;
            int dst = 0;
            while (dst < output.Length) {
                byte flag = ReadByte(input, ref src);
                bool isRun = (flag & 0x80) != 0;
                int length = (flag & 0x7F) + (isRun ? 3 : 1);
                if (length > output.Length - dst) {
                    throw new RomFormatException($"RLE run of {length} bytes overflows the output at offset {dst}.");
                }

                if (isRun) {
                    byte value = ReadByte(input, ref src);
                    output.AsSpan(dst, length).Fill(value);
                    dst += length;
                } else {
                    for (int i = 0; i < length; i++) {
                        output[dst++] = ReadByte(input, ref src);
                    }
                }
            }
            return src;
        }

        private static byte ReadByte(ReadOnlySpan<byte> input, ref int src) {
            if (src >= input.Length) {
                throw new RomFormatException("Compressed stream ended early.");
            }
            return input[src++];
        }
    }
}
