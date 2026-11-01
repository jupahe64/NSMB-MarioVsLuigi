using System.Text;
using Xunit;

namespace NsmbRom.Tests {
    public class ChecksumsTests {
        private static readonly byte[] CheckInput = Encoding.ASCII.GetBytes("123456789");

        [Fact]
        public void Crc16MatchesModbusCheckValue() {
            Assert.Equal(0x4B37, Checksums.Crc16(CheckInput));
        }

        [Fact]
        public void Crc32MatchesStandardCheckValue() {
            Assert.Equal(0xCBF43926u, Checksums.Crc32(CheckInput));
        }
    }
}
