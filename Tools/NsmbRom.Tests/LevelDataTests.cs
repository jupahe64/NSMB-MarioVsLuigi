using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using NsmbRom.Imaging;
using NsmbRom.Nitro;
using NsmbRom.Nsmb;
using Xunit;

namespace NsmbRom.Tests {
    public class ObjectDefinitionTests {
        private static byte[] Tile(byte control, int low, int high = 0) {
            return new[] { control, (byte) low, (byte) high };
        }

        private static byte[] Join(params object[] parts) {
            List<byte> data = new();
            foreach (object part in parts) {
                if (part is byte[] bytes) {
                    data.AddRange(bytes);
                } else {
                    data.Add(Convert.ToByte(part));
                }
            }
            return data.ToArray();
        }

        private static string Row(int[,] block, int y) {
            List<string> cells = new();
            for (int x = 0; x < block.GetLength(0); x++) {
                cells.Add(block[x, y].ToString());
            }
            return string.Join(",", cells);
        }

        [Fact]
        public void WithoutRepeatFlagsTheWholeDefinitionTiles() {
            byte[] data = Join(Tile(0, 1), Tile(0, 2), 0xFE, Tile(0, 3), Tile(0, 4), 0xFE, 0xFF);

            int[,] block = ObjectDefinition.Parse(data, 0).Render(3, 3);

            Assert.Equal("1,2,1", Row(block, 0));
            Assert.Equal("3,4,3", Row(block, 1));
            Assert.Equal("1,2,1", Row(block, 2));
        }

        [Fact]
        public void RepeatFlagsGiveHeadTiledMiddleAndTail() {
            byte[] data = Join(Tile(0, 1), Tile(1, 2), Tile(0, 3), 0xFE, 0xFF);

            int[,] block = ObjectDefinition.Parse(data, 0).Render(5, 1);

            Assert.Equal("1,2,2,2,3", Row(block, 0));
        }

        [Fact]
        public void VerticalRepeatUsesFirstTileOfEachRow() {
            byte[] data = Join(Tile(0, 1), 0xFE, Tile(2, 2), 0xFE, Tile(0, 3), 0xFE, 0xFF);

            int[,] block = ObjectDefinition.Parse(data, 0).Render(1, 4);

            Assert.Equal(new[] { "1", "2", "2", "3" }, new[] { Row(block, 0), Row(block, 1), Row(block, 2), Row(block, 3) });
        }

        [Fact]
        public void ZeroTileIsAnEmptyCell() {
            byte[] data = Join(Tile(0, 0), 0xFE, 0xFF);

            Assert.Equal(ObjectDefinition.EmptyCell, ObjectDefinition.Parse(data, 0).Render(1, 1)[0, 0]);
        }

        [Fact]
        public void TilesKeepTheirTilesetAcrossTheGlobalNumbering() {
            byte[] data = Join(Tile(0, 5, 1), Tile(0, 6, 4), Tile(0x40, 7), Tile(0x40, 8, 1), 0xFE, 0xFF);

            int[,] block = ObjectDefinition.Parse(data, 0).Render(4, 1);

            Assert.Equal(TilesetSlot.Level, LevelGrid.SplitCell(block[0, 0], out int level));
            Assert.Equal(5, level);
            Assert.Equal(TilesetSlot.Sub, LevelGrid.SplitCell(block[1, 0], out int sub));
            Assert.Equal(6, sub);
            Assert.Equal(TilesetSlot.Common, LevelGrid.SplitCell(block[2, 0], out int extended));
            Assert.Equal(775, extended);
            Assert.Equal(TilesetSlot.Level, LevelGrid.SplitCell(block[3, 0], out int levelExtended));
            Assert.Equal(776, levelExtended);
        }

        [Fact]
        public void SlopeClimbsFromBottomLeft() {
            byte[] data = Join(0x80, Tile(0, 1), 0xFE, 0xFF);

            int[,] block = ObjectDefinition.Parse(data, 0).Render(3, 3);

            Assert.Equal("-2,-2,1", Row(block, 0));
            Assert.Equal("-2,1,-2", Row(block, 1));
            Assert.Equal("1,-2,-2", Row(block, 2));
        }

        [Fact]
        public void SlopeGoingLeftClimbsFromBottomRight() {
            byte[] data = Join(0x81, Tile(0, 1), 0xFE, 0xFF);

            int[,] block = ObjectDefinition.Parse(data, 0).Render(2, 2);

            Assert.Equal("1,-2", Row(block, 0));
            Assert.Equal("-2,1", Row(block, 1));
        }

        [Fact]
        public void SlopeSubBlockSitsUnderTheMainBlock() {
            byte[] data = Join(0x80, Tile(0, 1), 0xFE, 0x85, Tile(4, 2), 0xFE, 0xFF);

            int[,] block = ObjectDefinition.Parse(data, 0).Render(2, 3);

            Assert.Equal("-2,-2", Row(block, 0));
            Assert.Equal("-2,1", Row(block, 1));
            Assert.Equal("1,2", Row(block, 2));
        }
    }

    public class LevelAreaTests {
        private static byte[] BuildLevel(Dictionary<int, byte[]> blocks) {
            const int headerSize = 14 * 8;
            List<byte> body = new();
            byte[] table = new byte[headerSize];
            for (int i = 0; i < 14; i++) {
                byte[] block = blocks.TryGetValue(i, out byte[]? value) ? value : Array.Empty<byte>();
                BinaryPrimitives.WriteInt32LittleEndian(table.AsSpan(i * 8), headerSize + body.Count);
                BinaryPrimitives.WriteInt32LittleEndian(table.AsSpan((i * 8) + 4), block.Length);
                body.AddRange(block);
            }

            List<byte> level = new(table);
            level.AddRange(body);
            return level.ToArray();
        }

        private static Dictionary<int, byte[]> MinimalBlocks() {
            byte[] settings = new byte[0x20];
            settings[0x0C] = 7;
            settings[2] = 0x21;
            return new Dictionary<int, byte[]> {
                [0] = settings,
                [2] = new byte[] { 0, 0, 9, 0 },
                [6] = new byte[4],
            };
        }

        [Fact]
        public void ReadsSettings() {
            LevelArea area = LevelArea.Parse("T", BuildLevel(MinimalBlocks()), Array.Empty<byte>());

            Assert.Equal(7, area.Settings.Tileset);
            Assert.True(area.Settings.Wraps);
            Assert.True(area.Settings.ForceMini);
            Assert.False(area.Settings.MiniPhysics);
            Assert.Equal(9, area.BackgroundScreen);
        }

        [Fact]
        public void ReadsSpriteDataInGameOrder() {
            Dictionary<int, byte[]> blocks = MinimalBlocks();
            blocks[6] = new byte[] { 57, 0, 10, 0, 20, 0, 1, 2, 3, 4, 5, 6, 0xFF, 0xFF, 0xFF, 0xFF };

            LevelSprite sprite = Assert.Single(LevelArea.Parse("T", BuildLevel(blocks), Array.Empty<byte>()).Sprites);

            Assert.Equal(57, sprite.Type);
            Assert.Equal(10, sprite.X);
            Assert.Equal(20, sprite.Y);
            Assert.Equal(new[] { 2, 1, 6, 5, 4, 3 }, sprite.Data);
        }

        [Fact]
        public void ReadsObjectsWithNegativePositions() {
            byte[] bgdat = { 0x05, 0x10, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x01, 0x00, 0xFF, 0xFF };

            LevelObject placed = Assert.Single(LevelArea.Parse("T", BuildLevel(MinimalBlocks()), bgdat).Objects);

            Assert.Equal(5, placed.Id);
            Assert.Equal(1, placed.Tileset);
            Assert.Equal(-1, placed.X);
            Assert.Equal(3, placed.Y);
        }

        [Fact]
        public void RefusesBlockPastEndOfFile() {
            byte[] level = BuildLevel(MinimalBlocks());
            BinaryPrimitives.WriteInt32LittleEndian(level.AsSpan(4), level.Length);

            Assert.Throws<RomFormatException>(() => LevelArea.Parse("T", level, Array.Empty<byte>()));
        }

        [Fact]
        public void RefusesMisalignedEntranceBlock() {
            Dictionary<int, byte[]> blocks = MinimalBlocks();
            blocks[5] = new byte[19];

            Assert.Throws<RomFormatException>(() => LevelArea.Parse("T", BuildLevel(blocks), Array.Empty<byte>()));
        }
    }

    public class NsbtxTests {
        private static byte[] BuildNsbtx() {
            const int tex0 = 0x14;
            const int info = 0x40;
            const int textureData = 0x70;
            const int paletteInfo = 0x90;
            const int paletteData = 0xC0;
            byte[] file = new byte[tex0 + paletteData + 8];

            "BTX0"u8.CopyTo(file);
            BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(0x10), tex0);
            "TEX0"u8.CopyTo(file.AsSpan(tex0));
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(tex0 + 0x0E), info);
            BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(tex0 + 0x14), textureData);
            BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(tex0 + 0x30), 1);
            BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(tex0 + 0x34), paletteInfo);
            BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(tex0 + 0x38), paletteData);

            WriteDictionary(file, tex0 + info, "tex", (0 | (0u << 20) | (0u << 23) | (3u << 26) | (1u << 29)));
            WriteDictionary(file, tex0 + paletteInfo, "pal", 0);

            // 8x8 16-colour texture: first pixel index 1, second index 0, rest 0.
            file[tex0 + textureData] = 0x01;
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(tex0 + paletteData + 2), 0x001F);
            return file;
        }

        private static void WriteDictionary(byte[] file, int dictionary, string name, uint entry) {
            file[dictionary + 1] = 1;
            const int headerOffset = 0x10;
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(dictionary + 6), headerOffset);
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(dictionary + headerOffset), 8);
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(dictionary + headerOffset + 2), 12);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(dictionary + headerOffset + 4), entry);
            System.Text.Encoding.ASCII.GetBytes(name).CopyTo(file, dictionary + headerOffset + 12);
        }

        [Fact]
        public void ReadsDictionariesAndDecodes16ColourTexture() {
            Nsbtx nsbtx = Nsbtx.Parse(BuildNsbtx());
            NsbtxTexture texture = Assert.Single(nsbtx.Textures);

            Assert.Equal("tex", texture.Name);
            Assert.Equal(8, texture.Width);
            Assert.Equal(3, texture.Format);
            Assert.True(texture.Color0Transparent);

            byte[] rgba = nsbtx.Decode(texture, nsbtx.Palettes[0])!;
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, rgba[..4]);
            Assert.Equal(0, rgba[7]);
        }

        [Fact]
        public void RefusesWrongMagic() {
            Assert.Throws<RomFormatException>(() => Nsbtx.Parse(new byte[0x100]));
        }
    }

    public class PngWriterTests {
        [Fact]
        public void WritesSignatureHeaderAndTrailer() {
            using MemoryStream output = new();
            PngWriter.Write(output, new byte[2 * 3 * 4], 2, 3);
            byte[] png = output.ToArray();

            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
            Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
            Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
            Assert.Equal("IEND"u8.ToArray(), png[^8..^4]);
        }

        [Fact]
        public void RefusesWrongBufferSize() {
            using MemoryStream output = new();

            Assert.Throws<ArgumentException>(() => PngWriter.Write(output, new byte[3], 1, 1));
        }
    }
}
