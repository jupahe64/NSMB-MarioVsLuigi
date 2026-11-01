using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NsmbRom.Imaging {
    /// <summary>Minimal 8-bit RGBA PNG encoder, so the tools need no image library.</summary>
    public static class PngWriter {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private const int BytesPerPixel = 4;

        public static void Write(Stream output, byte[] rgba, int width, int height) {
            if (rgba.Length != width * height * BytesPerPixel) {
                throw new ArgumentException("Pixel buffer does not match the image size.", nameof(rgba));
            }

            output.Write(Signature, 0, Signature.Length);

            byte[] header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
            header[8] = 8;
            header[9] = 6;
            WriteChunk(output, "IHDR", header);
            WriteChunk(output, "IDAT", Compress(rgba, width, height));
            WriteChunk(output, "IEND", Array.Empty<byte>());
        }

        private static byte[] Compress(byte[] rgba, int width, int height) {
            int stride = width * BytesPerPixel;
            using MemoryStream result = new();
            result.WriteByte(0x78);
            result.WriteByte(0x01);

            uint adlerA = 1;
            uint adlerB = 0;
            byte[] row = new byte[stride + 1];
            using (DeflateStream deflate = new(result, CompressionLevel.Optimal, leaveOpen: true)) {
                for (int y = 0; y < height; y++) {
                    Buffer.BlockCopy(rgba, y * stride, row, 1, stride);
                    deflate.Write(row, 0, row.Length);
                    foreach (byte b in row) {
                        adlerA = (adlerA + b) % 65521;
                        adlerB = (adlerB + adlerA) % 65521;
                    }
                }
            }

            byte[] adler = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(adler, (adlerB << 16) | adlerA);
            result.Write(adler, 0, adler.Length);
            return result.ToArray();
        }

        private static void WriteChunk(Stream output, string type, byte[] data) {
            byte[] chunk = new byte[4 + data.Length];
            Encoding.ASCII.GetBytes(type).CopyTo(chunk, 0);
            data.CopyTo(chunk, 4);

            byte[] number = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
            output.Write(number, 0, number.Length);
            output.Write(chunk, 0, chunk.Length);
            BinaryPrimitives.WriteUInt32BigEndian(number, Checksums.Crc32(chunk));
            output.Write(number, 0, number.Length);
        }
    }
}
