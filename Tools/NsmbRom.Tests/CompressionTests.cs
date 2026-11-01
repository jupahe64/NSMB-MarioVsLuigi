using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using NsmbRom.Compression;
using Xunit;

namespace NsmbRom.Tests {
    public class NitroCompressionTests {
        [Fact]
        public void Lz77DecodesLiteralsAndOverlappingBackReference() {
            byte[] stream = { 0x10, 0x09, 0x00, 0x00, 0b0010_0000, (byte) 'A', (byte) 'B', 0x40, 0x01 };

            Assert.Equal("ABABABABA"u8.ToArray(), NitroCompression.Decompress(stream));
        }

        [Fact]
        public void Lz11DecodesShortAndMediumRuns() {
            // Literal, then a 2-byte token (length 3) and a 3-byte token (length 0x11).
            byte[] stream = { 0x11, 0x15, 0x00, 0x00, 0b0110_0000, (byte) 'X', 0x20, 0x00, 0x00, 0x00, 0x00 };

            Assert.Equal(new string('X', 0x15), Encoding.ASCII.GetString(NitroCompression.Decompress(stream)));
        }

        [Fact]
        public void RleDecodesRunsAndLiterals() {
            byte[] stream = { 0x30, 0x06, 0x00, 0x00, 0x80, (byte) 'Z', 0x02, (byte) 'a', (byte) 'b', (byte) 'c' };

            Assert.Equal("ZZZabc"u8.ToArray(), NitroCompression.Decompress(stream));
        }

        [Fact]
        public void RefusesBackReferenceBeforeStart() {
            byte[] stream = { 0x10, 0x05, 0x00, 0x00, 0b1000_0000, 0x10, 0x00 };

            Assert.Throws<RomFormatException>(() => NitroCompression.Decompress(stream));
        }

        [Fact]
        public void RefusesTruncatedStream() {
            byte[] stream = { 0x10, 0x20, 0x00, 0x00, 0x00, (byte) 'A' };

            Assert.Throws<RomFormatException>(() => NitroCompression.Decompress(stream));
        }

        [Fact]
        public void TryDecompressRejectsUncompressedDataAndLongTails() {
            Assert.False(NitroCompression.TryDecompress("plain text data"u8, out _));

            byte[] tail = new byte[0x40];
            new byte[] { 0x10, 0x01, 0x00, 0x00, 0x00, (byte) 'A' }.CopyTo(tail, 0);
            Assert.False(NitroCompression.TryDecompress(tail, out _));
        }
    }

    public class BackwardLzTests {
        /// <summary>Wraps a hand-written stream in a footer. The stream is read backwards, so tokens are listed last to first.</summary>
        private static byte[] Pack(byte[] prefix, byte[] stream, int decodedSize) {
            List<byte> result = new(prefix);
            result.AddRange(stream);
            while ((result.Count - prefix.Length + 8) % 4 != 0) {
                result.Add(0xFF);
            }
            int headerSize = result.Count - prefix.Length - stream.Length + 8;
            int regionSize = result.Count - prefix.Length + 8;
            result.AddRange(new byte[8]);

            byte[] data = result.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(data.Length - 8), (uint) regionSize | ((uint) headerSize << 24));
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(data.Length - 4), (uint) (decodedSize - regionSize));
            return data;
        }

        private static string Repeat(string text, int count) {
            return new StringBuilder().Insert(0, text, count).ToString();
        }

        [Fact]
        public void DecodesMinimumDistanceMatchAndKeepsPrefix() {
            // Flags: three literals, then a match of 18 at distance 3.
            byte[] stream = { 0x00, 0xF0, (byte) 'a', (byte) 'b', (byte) 'c', 0b0001_0000 };
            byte[] packed = Pack(new byte[] { 1, 2, 3 }, stream, 21);

            byte[] result = BackwardLz.Decompress(packed);

            Assert.Equal(new byte[] { 1, 2, 3 }, result[..3]);
            Assert.Equal(Repeat("abc", 7), Encoding.ASCII.GetString(result[3..]));
        }

        [Fact]
        public void DecodesMatchAtLongerDistance() {
            // Flags: six literals, then a match of 18 at distance 6.
            byte[] stream = { 0x03, 0xF0, (byte) 'u', (byte) 'v', (byte) 'w', (byte) 'x', (byte) 'y', (byte) 'z', 0b0000_0010 };
            byte[] packed = Pack(Array.Empty<byte>(), stream, 24);

            Assert.Equal(Repeat("uvwxyz", 4), Encoding.ASCII.GetString(BackwardLz.Decompress(packed)));
        }

        [Fact]
        public void RefusesStreamThatDoesNotFillTheOutput() {
            byte[] stream = { 0x00, 0xF0, (byte) 'a', (byte) 'b', (byte) 'c', 0b0001_0000 };
            byte[] packed = Pack(Array.Empty<byte>(), stream, 30);

            Assert.Throws<RomFormatException>(() => BackwardLz.Decompress(packed));
        }

        [Fact]
        public void RefusesBadFooter() {
            byte[] data = new byte[16];
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 0xFFFFFF | (8u << 24));

            Assert.Throws<RomFormatException>(() => BackwardLz.Decompress(data));
        }
    }
}
