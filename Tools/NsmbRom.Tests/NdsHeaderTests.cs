using System;
using System.Buffers.Binary;
using System.Text;
using NsmbRom.Nds;
using Xunit;

namespace NsmbRom.Tests {
    public class NdsHeaderTests {
        private const int RomSize = 0x4000;

        private static byte[] BuildRom(string gameCode = RomRegions.UsaGameCode) {
            byte[] rom = new byte[RomSize];
            Encoding.ASCII.GetBytes("NEW MARIO").CopyTo(rom, 0x00);
            Encoding.ASCII.GetBytes(gameCode).CopyTo(rom, 0x0C);
            Encoding.ASCII.GetBytes("01").CopyTo(rom, 0x10);
            rom[0x14] = 8;
            rom[0x1E] = 1;
            WriteSection(rom, 0x20, 0x2C, 0x400, 0x100);
            WriteSection(rom, 0x30, 0x3C, 0x500, 0x100);
            WriteSection(rom, 0x40, 0x44, 0x600, 0x100);
            WriteSection(rom, 0x48, 0x4C, 0x700, 8 * 16);
            WriteSection(rom, 0x50, 0x54, 0x800, 32 * 3);
            WriteU32(rom, 0x80, RomSize);
            FixCrc(rom);
            return rom;
        }

        private static void WriteSection(byte[] rom, int offsetField, int sizeField, uint offset, uint size) {
            WriteU32(rom, offsetField, offset);
            WriteU32(rom, sizeField, size);
        }

        private static void WriteU32(byte[] rom, int offset, uint value) {
            BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(offset), value);
        }

        private static void FixCrc(byte[] rom) {
            BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(0x15E), Checksums.Crc16(rom.AsSpan(0, 0x15E)));
        }

        [Fact]
        public void ParsesFieldsAndDerivedCounts() {
            NdsHeader header = NdsHeader.Parse(BuildRom());

            Assert.Equal("NEW MARIO", header.Title);
            Assert.Equal("A2DE", header.GameCode);
            Assert.Equal("01", header.MakerCode);
            Assert.Equal(1, header.RomVersion);
            Assert.Equal(0x2000000L, header.DeviceCapacity);
            Assert.Equal(16, header.FileCount);
            Assert.Equal(3, header.Arm9OverlayCount);
            Assert.Equal(0, header.Arm7OverlayCount);
            Assert.Equal(0x400u, header.Arm9.Offset);
        }

        [Theory]
        [InlineData("A2DE", RomRegion.Usa)]
        [InlineData("A2DP", RomRegion.Europe)]
        public void DetectsSupportedRegions(string gameCode, RomRegion region) {
            NdsHeader header = NdsHeader.Parse(BuildRom(gameCode));

            Assert.Equal(region, RomRegions.FromGameCode(header.GameCode));
        }

        [Theory]
        [InlineData("A2DJ")]
        [InlineData("ASME")]
        public void RefusesUnsupportedGameCodes(string gameCode) {
            Assert.Throws<RomFormatException>(() => NdsHeader.Parse(BuildRom(gameCode)));
        }

        [Fact]
        public void RefusesTruncatedFile() {
            Assert.Throws<RomFormatException>(() => NdsHeader.Parse(new byte[0x100]));
        }

        [Fact]
        public void RefusesBadHeaderCrc() {
            byte[] rom = BuildRom();
            rom[0x00] ^= 0xFF;

            Assert.Throws<RomFormatException>(() => NdsHeader.Parse(rom));
        }

        [Fact]
        public void RefusesSectionPastEndOfFile() {
            byte[] rom = BuildRom();
            WriteU32(rom, 0x44, RomSize);
            FixCrc(rom);

            Assert.Throws<RomFormatException>(() => NdsHeader.Parse(rom));
        }

        [Fact]
        public void RefusesMisalignedFat() {
            byte[] rom = BuildRom();
            WriteU32(rom, 0x4C, 9);
            FixCrc(rom);

            Assert.Throws<RomFormatException>(() => NdsHeader.Parse(rom));
        }
    }
}
