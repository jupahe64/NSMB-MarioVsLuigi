using System;

namespace NsmbRom {
    public static class Checksums {
        private static readonly uint[] Crc32Table = BuildCrc32Table();

        /// <summary>CRC-16/MODBUS, the variant used by the NDS header.</summary>
        public static ushort Crc16(ReadOnlySpan<byte> data) {
            uint crc = 0xFFFF;
            foreach (byte b in data) {
                crc ^= b;
                for (int i = 0; i < 8; i++) {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1;
                }
            }
            return (ushort) crc;
        }

        public static uint Crc32(ReadOnlySpan<byte> data) {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data) {
                crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            return ~crc;
        }

        private static uint[] BuildCrc32Table() {
            uint[] table = new uint[256];
            for (uint i = 0; i < table.Length; i++) {
                uint crc = i;
                for (int bit = 0; bit < 8; bit++) {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
                }
                table[i] = crc;
            }
            return table;
        }
    }
}
