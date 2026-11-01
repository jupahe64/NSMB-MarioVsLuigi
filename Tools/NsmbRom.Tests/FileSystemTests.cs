using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using NsmbRom.Nds;
using NsmbRom.Nitro;
using Xunit;

namespace NsmbRom.Tests {
    public class NitroFileTableTests {
        // Root holds a.bin and dir/, dir/ holds b.bin. File IDs start at 2, as if two overlays came first.
        private static byte[] BuildFnt() {
            List<byte> fnt = new();
            AddU32(fnt, 16);
            AddU16(fnt, 2);
            AddU16(fnt, 2);
            AddU32(fnt, 29);
            AddU16(fnt, 3);
            AddU16(fnt, 0xF000);

            fnt.Add(5);
            fnt.AddRange(Encoding.ASCII.GetBytes("a.bin"));
            fnt.Add(0x83);
            fnt.AddRange(Encoding.ASCII.GetBytes("dir"));
            AddU16(fnt, 0xF001);
            fnt.Add(0);

            fnt.Add(5);
            fnt.AddRange(Encoding.ASCII.GetBytes("b.bin"));
            fnt.Add(0);
            return fnt.ToArray();
        }

        private static byte[] BuildFat(int files) {
            List<byte> fat = new();
            for (int i = 0; i < files; i++) {
                AddU32(fat, (uint) (i * 16));
                AddU32(fat, (uint) ((i * 16) + 8));
            }
            return fat.ToArray();
        }

        private static void AddU32(List<byte> list, uint value) {
            list.AddRange(BitConverter.GetBytes(value));
        }

        private static void AddU16(List<byte> list, ushort value) {
            list.AddRange(BitConverter.GetBytes(value));
        }

        [Fact]
        public void ResolvesPathsAndLeavesOverlaysUnnamed() {
            NitroFileTable table = NitroFileTable.Parse(BuildFnt(), BuildFat(4));

            Assert.Null(table.Files[0].Path);
            Assert.Null(table.Files[1].Path);
            Assert.Equal("a.bin", table.Files[2].Path);
            Assert.Equal("dir/b.bin", table.Files[3].Path);
            Assert.Equal(3, table.ByPath["dir/b.bin"].Id);
        }

        [Fact]
        public void RefusesFileIdPastFat() {
            Assert.Throws<RomFormatException>(() => NitroFileTable.Parse(BuildFnt(), BuildFat(3)));
        }

        [Fact]
        public void RefusesInvertedFatRange() {
            byte[] fat = BuildFat(4);
            BinaryPrimitives.WriteUInt32LittleEndian(fat.AsSpan(4), 0);
            fat[0] = 8;

            Assert.Throws<RomFormatException>(() => NitroFileTable.Parse(BuildFnt(), fat));
        }

        [Theory]
        [InlineData("..")]
        [InlineData("a/b")]
        [InlineData("a\\b")]
        public void RefusesUnsafeNames(string name) {
            List<byte> fnt = new();
            AddU32(fnt, 8);
            AddU16(fnt, 0);
            AddU16(fnt, 1);
            fnt.Add((byte) name.Length);
            fnt.AddRange(Encoding.ASCII.GetBytes(name));
            fnt.Add(0);

            Assert.Throws<RomFormatException>(() => NitroFileTable.Parse(fnt.ToArray(), BuildFat(1)));
        }

        [Fact]
        public void RefusesDirectoryLoop() {
            List<byte> fnt = new();
            AddU32(fnt, 8);
            AddU16(fnt, 0);
            AddU16(fnt, 1);
            fnt.Add(0x81);
            fnt.Add((byte) 'x');
            AddU16(fnt, 0xF000);
            fnt.Add(0);

            Assert.Throws<RomFormatException>(() => NitroFileTable.Parse(fnt.ToArray(), BuildFat(1)));
        }
    }

    public class NarcTests {
        private static byte[] BuildNarc() {
            List<byte> narc = new();
            narc.AddRange(Encoding.ASCII.GetBytes("NARC"));
            narc.AddRange(new byte[] { 0xFE, 0xFF, 0x00, 0x01 });
            narc.AddRange(new byte[4]);
            narc.AddRange(new byte[] { 0x10, 0x00, 0x03, 0x00 });

            narc.AddRange(Encoding.ASCII.GetBytes("BTAF"));
            narc.AddRange(BitConverter.GetBytes(12u + 16u));
            narc.AddRange(BitConverter.GetBytes(2u));
            narc.AddRange(BitConverter.GetBytes(0u));
            narc.AddRange(BitConverter.GetBytes(4u));
            narc.AddRange(BitConverter.GetBytes(4u));
            narc.AddRange(BitConverter.GetBytes(6u));

            narc.AddRange(Encoding.ASCII.GetBytes("BTNF"));
            narc.AddRange(BitConverter.GetBytes(8u + 12u));
            narc.AddRange(BitConverter.GetBytes(8u));
            narc.AddRange(new byte[] { 0, 0, 1, 0 });
            narc.AddRange(new byte[4]);

            narc.AddRange(Encoding.ASCII.GetBytes("GMIF"));
            narc.AddRange(BitConverter.GetBytes(8u + 8u));
            narc.AddRange(Encoding.ASCII.GetBytes("abcdefgh"));

            return narc.ToArray();
        }

        [Fact]
        public void ReadsFilesRelativeToDataChunk() {
            Narc narc = Narc.Parse(BuildNarc());

            Assert.Equal(2, narc.FileTable.Files.Count);
            Assert.Equal("abcd"u8.ToArray(), narc.GetFileData(narc.FileTable.Files[0]).ToArray());
            Assert.Equal("ef"u8.ToArray(), narc.GetFileData(narc.FileTable.Files[1]).ToArray());
        }

        [Fact]
        public void RefusesWrongMagic() {
            byte[] data = BuildNarc();
            data[0] = (byte) 'X';

            Assert.Throws<RomFormatException>(() => Narc.Parse(data));
        }

        [Fact]
        public void RefusesFilePastDataChunk() {
            byte[] data = BuildNarc();
            int chunk = Encoding.ASCII.GetString(data).IndexOf("BTAF", StringComparison.Ordinal);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(chunk + 24), 0x1000);

            Assert.Throws<RomFormatException>(() => Narc.Parse(data));
        }
    }
}
