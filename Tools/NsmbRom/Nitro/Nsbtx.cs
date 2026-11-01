using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace NsmbRom.Nitro {
    public sealed class NsbtxTexture {
        public string Name { get; internal set; } = "";
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public int Format { get; internal set; }
        public bool Color0Transparent { get; internal set; }
        internal int DataOffset { get; set; }
    }

    public sealed class NsbtxPalette {
        public string Name { get; internal set; } = "";
        internal int DataOffset { get; set; }
        public int Size { get; internal set; }

        /// <summary>A view of part of this palette, for files that pack several palettes into one entry.</summary>
        public NsbtxPalette Slice(int offset, int size) {
            return new NsbtxPalette { Name = Name, DataOffset = DataOffset + offset, Size = size };
        }
    }

    /// <summary>A Nitro texture container. Compressed 4x4 textures are listed but cannot be decoded yet.</summary>
    public sealed class Nsbtx {
        public const int Format4x4 = 5;
        private const int FormatA3I5 = 1;
        private const int Format4Color = 2;
        private const int Format16Color = 3;
        private const int Format256Color = 4;
        private const int FormatA5I3 = 6;
        private const int FormatDirect = 7;

        private const int NameSize = 16;
        private const int MinSize = 0x54;

        private readonly byte[] data;

        public List<NsbtxTexture> Textures { get; } = new();
        public List<NsbtxPalette> Palettes { get; } = new();

        private Nsbtx(byte[] data) {
            this.data = data;
        }

        public static bool HasMagic(ReadOnlySpan<byte> data) {
            return data.Length >= 4 && Encoding.ASCII.GetString(data.Slice(0, 4)) == "BTX0";
        }

        public static Nsbtx Parse(byte[] data) {
            if (!HasMagic(data) || data.Length < MinSize) {
                throw new RomFormatException("Not an NSBTX file.");
            }

            int tex0 = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0x10));
            if (tex0 < 0 || tex0 + 0x40 > data.Length || Encoding.ASCII.GetString(data, tex0, 4) != "TEX0") {
                throw new RomFormatException("NSBTX has no TEX0 block.");
            }

            Nsbtx result = new(data);
            int textureInfo = tex0 + U16(data, tex0 + 0x0E);
            int textureData = tex0 + I32(data, tex0 + 0x14);
            int paletteSize = I32(data, tex0 + 0x30) << 3;
            int paletteInfo = tex0 + I32(data, tex0 + 0x34);
            int paletteData = tex0 + I32(data, tex0 + 0x38);

            foreach ((string name, int entry) in ReadDictionary(data, textureInfo)) {
                uint parameters = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(entry));
                result.Textures.Add(new NsbtxTexture {
                    Name = name,
                    DataOffset = textureData + ((int) (parameters & 0xFFFF) << 3),
                    Width = 8 << (int) ((parameters >> 20) & 7),
                    Height = 8 << (int) ((parameters >> 23) & 7),
                    Format = (int) ((parameters >> 26) & 7),
                    Color0Transparent = ((parameters >> 29) & 1) != 0,
                });
            }

            foreach ((string name, int entry) in ReadDictionary(data, paletteInfo)) {
                result.Palettes.Add(new NsbtxPalette { Name = name, DataOffset = paletteData + (U16(data, entry) << 3) });
            }
            for (int i = 0; i < result.Palettes.Count; i++) {
                int end = i + 1 < result.Palettes.Count ? result.Palettes[i + 1].DataOffset : paletteData + paletteSize;
                result.Palettes[i].Size = end - result.Palettes[i].DataOffset;
            }
            return result;
        }

        /// <summary>Decodes a texture to RGBA, or returns null for formats that are not supported yet.</summary>
        public byte[]? Decode(NsbtxTexture texture, NsbtxPalette? palette) {
            int pixels = texture.Width * texture.Height;
            byte[] rgba = new byte[pixels * 4];
            for (int i = 0; i < pixels; i++) {
                int offset = texture.DataOffset;
                switch (texture.Format) {
                case FormatA3I5:
                    byte a3i5 = ByteAt(offset + i);
                    WritePaletted(rgba, i, palette, a3i5 & 0x1F, Alpha(a3i5 >> 5, 7));
                    break;
                case FormatA5I3:
                    byte a5i3 = ByteAt(offset + i);
                    WritePaletted(rgba, i, palette, a5i3 & 7, Alpha(a5i3 >> 3, 31));
                    break;
                case Format4Color:
                    int color4 = (ByteAt(offset + (i >> 2)) >> ((i & 3) * 2)) & 3;
                    WritePaletted(rgba, i, palette, color4, color4 == 0 && texture.Color0Transparent ? 0 : 255);
                    break;
                case Format16Color:
                    int color16 = (ByteAt(offset + (i >> 1)) >> ((i & 1) * 4)) & 15;
                    WritePaletted(rgba, i, palette, color16, color16 == 0 && texture.Color0Transparent ? 0 : 255);
                    break;
                case Format256Color:
                    int color256 = ByteAt(offset + i);
                    WritePaletted(rgba, i, palette, color256, color256 == 0 && texture.Color0Transparent ? 0 : 255);
                    break;
                case FormatDirect:
                    int word = ByteAt(offset + (i * 2)) | (ByteAt(offset + (i * 2) + 1) << 8);
                    WriteColor(rgba, i, word, (word & 0x8000) != 0 ? 255 : 0);
                    break;
                default:
                    return null;
                }
            }
            return rgba;
        }

        private void WritePaletted(byte[] rgba, int pixel, NsbtxPalette? palette, int index, int alpha) {
            if (palette == null || (index * 2) + 2 > palette.Size) {
                throw new RomFormatException($"Palette index {index} is out of range.");
            }
            int offset = palette.DataOffset + (index * 2);
            WriteColor(rgba, pixel, ByteAt(offset) | (ByteAt(offset + 1) << 8), alpha);
        }

        private static void WriteColor(byte[] rgba, int pixel, int color, int alpha) {
            rgba[pixel * 4] = Expand(color & 31);
            rgba[(pixel * 4) + 1] = Expand((color >> 5) & 31);
            rgba[(pixel * 4) + 2] = Expand((color >> 10) & 31);
            rgba[(pixel * 4) + 3] = (byte) alpha;
        }

        private static byte Expand(int channel) {
            return (byte) ((channel << 3) | (channel >> 2));
        }

        private static int Alpha(int value, int max) {
            return value * 255 / max;
        }

        private byte ByteAt(int offset) {
            if (offset < 0 || offset >= data.Length) {
                throw new RomFormatException($"NSBTX data offset 0x{offset:X} is out of range.");
            }
            return data[offset];
        }

        private static IEnumerable<(string Name, int Entry)> ReadDictionary(byte[] data, int dictionary) {
            int count = data[dictionary + 1];
            int header = dictionary + U16(data, dictionary + 6);
            int entrySize = U16(data, header);
            int names = header + U16(data, header + 2);
            for (int i = 0; i < count; i++) {
                string name = Encoding.ASCII.GetString(data, names + (i * NameSize), NameSize).TrimEnd('\0');
                yield return (name, header + 4 + (i * entrySize));
            }
        }

        private static int U16(byte[] data, int offset) {
            return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
        }

        private static int I32(byte[] data, int offset) {
            return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        }
    }
}
