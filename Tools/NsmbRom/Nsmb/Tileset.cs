using System;
using System.Buffers.Binary;

namespace NsmbRom.Nsmb {
    public enum TilesetSlot {
        /// <summary>The common set, shared by every level.</summary>
        Common = 0,
        /// <summary>The per-level set.</summary>
        Level = 1,
        /// <summary>The sub-unit set, shared by every level.</summary>
        Sub = 2,
    }

    /// <summary>One of the three tilesets a level uses: 8bpp graphics, palettes, Map16, behaviours and object definitions.</summary>
    public sealed class Tileset {
        public const int TilePixels = 16;
        public const int AtlasColumns = 16;
        private const int CharPixels = 8;
        private const int CharBytes = CharPixels * CharPixels;
        private const int ColorsPerPalette = 256;
        private const int Map16TileBytes = 8;
        private const int ObjectIndexEntryBytes = 4;
        private const int MaxObjects = 256;

        // Map16 words index the whole BG VRAM; each slot's graphics and palettes start at a fixed place in it.
        private static readonly int[] CharOffsets = { 0, 192, 640 };
        private static readonly int[] PaletteOffsets = { 0, 2, 6 };

        private readonly byte[] graphics;
        private readonly byte[] palettes;
        private readonly byte[] map16;
        private readonly TileObjectDefinition?[] objects;

        public TilesetSlot Slot { get; }
        public uint[] Behaviors { get; }
        public int TileCount => map16.Length / Map16TileBytes;
        public int ObjectCount => objects.Length;
        public int PaletteCount => palettes.Length / (ColorsPerPalette * 2);
        public int BadCharReferences { get; private set; }

        public Tileset(TilesetSlot slot, byte[] graphics, byte[] palettes, byte[] map16, byte[] objectIndex, byte[] objectData, byte[] behaviors) {
            if (graphics.Length % CharBytes != 0 || palettes.Length == 0 || palettes.Length % (ColorsPerPalette * 2) != 0 || map16.Length % Map16TileBytes != 0) {
                throw new RomFormatException($"Tileset slot {slot} has inconsistent sizes: graphics 0x{graphics.Length:X}, palettes 0x{palettes.Length:X}, Map16 0x{map16.Length:X}.");
            }

            Slot = slot;
            this.graphics = graphics;
            this.palettes = palettes;
            this.map16 = map16;

            Behaviors = new uint[behaviors.Length / 4];
            for (int i = 0; i < Behaviors.Length; i++) {
                Behaviors[i] = BinaryPrimitives.ReadUInt32LittleEndian(behaviors.AsSpan(i * 4));
            }

            objects = new TileObjectDefinition?[Math.Min(objectIndex.Length / ObjectIndexEntryBytes, MaxObjects)];
            for (int i = 0; i < objects.Length; i++) {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(objectIndex.AsSpan(i * ObjectIndexEntryBytes));
                objects[i] = TileObjectDefinition.Parse(objectData, offset);
            }
        }

        /// <summary>True when the Map16 entry holds no graphics at all, which is the case for tiles the game fills in at runtime.</summary>
        public bool IsBlankTile(int tile) {
            return BinaryPrimitives.ReadUInt64LittleEndian(map16.AsSpan(tile * Map16TileBytes)) == 0;
        }

        public TileObjectDefinition? GetObject(int id) {
            return id >= 0 && id < objects.Length ? objects[id] : null;
        }

        /// <summary>All Map16 tiles as RGBA, <see cref="AtlasColumns"/> per row. Index 0 of each palette is transparent.</summary>
        public byte[] RenderAtlas(AreaTilesets vram, out int width, out int height) {
            int rows = (TileCount + AtlasColumns - 1) / AtlasColumns;
            width = AtlasColumns * TilePixels;
            height = rows * TilePixels;
            byte[] rgba = new byte[width * height * 4];

            BadCharReferences = 0;
            for (int tile = 0; tile < TileCount; tile++) {
                int originX = (tile % AtlasColumns) * TilePixels;
                int originY = (tile / AtlasColumns) * TilePixels;
                for (int quadrant = 0; quadrant < 4; quadrant++) {
                    ushort word = BinaryPrimitives.ReadUInt16LittleEndian(map16.AsSpan((tile * Map16TileBytes) + (quadrant * 2)));
                    DrawChar(vram, rgba, width, originX + ((quadrant & 1) * CharPixels), originY + ((quadrant >> 1) * CharPixels), word);
                }

                byte[]? pipe = tile < Behaviors.Length ? vram.Pipes.GetTile(Slot, tile, Behaviors[tile]) : null;
                if (pipe != null) {
                    for (int y = 0; y < TilePixels; y++) {
                        Buffer.BlockCopy(pipe, y * TilePixels * 4, rgba, (((originY + y) * width) + originX) * 4, TilePixels * 4);
                    }
                }
            }
            return rgba;
        }

        // Words address the BG VRAM shared by all three sets, so any tile may use another set's chars or palettes.
        private void DrawChar(AreaTilesets vram, byte[] rgba, int stride, int destX, int destY, ushort word) {
            if (word == 0) {
                return;
            }

            int vramChar = word & 0x3FF;
            int vramPalette = word >> 12;
            Tileset charOwner = vram[FindSlot(CharOffsets, vramChar)];
            Tileset paletteOwner = vram[FindSlot(PaletteOffsets, vramPalette)];
            int charIndex = vramChar - CharOffsets[(int) charOwner.Slot];
            int palette = vramPalette - PaletteOffsets[(int) paletteOwner.Slot];
            int charStart = charIndex * CharBytes;
            if (charStart + CharBytes > charOwner.graphics.Length || palette >= paletteOwner.PaletteCount) {
                BadCharReferences++;
                return;
            }

            byte[] graphics = charOwner.graphics;
            byte[] palettes = paletteOwner.palettes;

            bool flipX = (word & 0x400) != 0;
            bool flipY = (word & 0x800) != 0;
            for (int y = 0; y < CharPixels; y++) {
                for (int x = 0; x < CharPixels; x++) {
                    int sourceX = flipX ? CharPixels - 1 - x : x;
                    int sourceY = flipY ? CharPixels - 1 - y : y;
                    int colorIndex = graphics[charStart + (sourceY * CharPixels) + sourceX];
                    if (colorIndex == 0) {
                        continue;
                    }

                    ushort color = BinaryPrimitives.ReadUInt16LittleEndian(palettes.AsSpan(((palette * ColorsPerPalette) + colorIndex) * 2));
                    int dest = (((destY + y) * stride) + destX + x) * 4;
                    rgba[dest] = Expand(color & 31);
                    rgba[dest + 1] = Expand((color >> 5) & 31);
                    rgba[dest + 2] = Expand((color >> 10) & 31);
                    rgba[dest + 3] = 255;
                }
            }
        }

        private static TilesetSlot FindSlot(int[] offsets, int value) {
            int slot = offsets.Length - 1;
            while (value < offsets[slot]) {
                slot--;
            }
            return (TilesetSlot) slot;
        }

        private static byte Expand(int channel) {
            return (byte) ((channel << 3) | (channel >> 2));
        }
    }
}
