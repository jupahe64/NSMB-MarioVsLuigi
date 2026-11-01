using System;
using System.Buffers.Binary;

namespace NsmbRom.Nsmb {
    /// <summary>
    /// A background or foreground: a 64-tile-wide map of 8x8 chars, drawn behind the level tiles.
    /// Both layers share BG VRAM, so a map can use chars of the other layer: backgrounds use this
    /// for the gradient rows between sky and ground, which live in the foreground graphics.
    /// </summary>
    public static class ScreenLayer {
        public const int BackgroundCharBase = 576;
        public const int BackgroundPaletteBase = 10;
        public const int ForegroundCharBase = 256;
        public const int ForegroundPaletteBase = 8;

        private const int WidthInChars = 64;
        private const int CharPixels = 8;
        private const int CharBytes = CharPixels * CharPixels;
        private const int ColorsPerPalette = 256;

        /// <summary>Renders a layer to RGBA, resolving char numbers through the graphics of both layers.</summary>
        public static byte[] Render(byte[]? backgroundChars, byte[]? foregroundChars, byte[] palettes, byte[] screen, int paletteBase, out int width, out int height) {
            int rows = screen.Length / 2 / WidthInChars;
            width = WidthInChars * CharPixels;
            height = rows * CharPixels;
            byte[] rgba = new byte[width * height * 4];

            for (int row = 0; row < rows; row++) {
                for (int column = 0; column < WidthInChars; column++) {
                    ushort word = BinaryPrimitives.ReadUInt16LittleEndian(screen.AsSpan(((row * WidthInChars) + column) * 2));
                    DrawChar(rgba, width, column * CharPixels, row * CharPixels, word, backgroundChars, foregroundChars, palettes, paletteBase);
                }
            }
            return rgba;
        }

        private static void DrawChar(byte[] rgba, int stride, int destX, int destY, ushort word, byte[]? backgroundChars, byte[]? foregroundChars, byte[] palettes, int paletteBase) {
            int number = word & 0x3FF;
            byte[]? chars = number >= BackgroundCharBase ? backgroundChars : foregroundChars;
            int charStart = (number - (number >= BackgroundCharBase ? BackgroundCharBase : ForegroundCharBase)) * CharBytes;
            int palette = (word >> 12) - paletteBase;
            if (chars == null || charStart < 0 || charStart + CharBytes > chars.Length || palette < 0) {
                return;
            }

            bool flipX = (word & 0x400) != 0;
            bool flipY = (word & 0x800) != 0;
            for (int y = 0; y < CharPixels; y++) {
                for (int x = 0; x < CharPixels; x++) {
                    int sourceX = flipX ? CharPixels - 1 - x : x;
                    int sourceY = flipY ? CharPixels - 1 - y : y;
                    int colorIndex = chars[charStart + (sourceY * CharPixels) + sourceX];
                    int colorOffset = ((palette * ColorsPerPalette) + colorIndex) * 2;
                    if (colorIndex == 0 || colorOffset + 2 > palettes.Length) {
                        continue;
                    }

                    ushort color = BinaryPrimitives.ReadUInt16LittleEndian(palettes.AsSpan(colorOffset));
                    int dest = (((destY + y) * stride) + destX + x) * 4;
                    rgba[dest] = Expand(color & 31);
                    rgba[dest + 1] = Expand((color >> 5) & 31);
                    rgba[dest + 2] = Expand((color >> 10) & 31);
                    rgba[dest + 3] = 255;
                }
            }
        }

        private static byte Expand(int channel) {
            return (byte) ((channel << 3) | (channel >> 2));
        }
    }
}
