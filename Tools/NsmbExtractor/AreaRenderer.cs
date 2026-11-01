using System;
using System.Collections.Generic;
using System.IO;
using NsmbRom.Imaging;
using NsmbRom.Nsmb;

namespace NsmbExtractor {
    /// <summary>Debug renders of expanded areas, cropped to the used cells, with markers for entrances and sprites.</summary>
    public sealed class AreaRenderer {
        private const int Tile = Tileset.TilePixels;
        private const int ContactCellWidth = 256;
        private const int ContactCellHeight = 128;
        private const int ContactColumns = 8;

        private static readonly byte[] Sky = { 0x60, 0x90, 0xF8, 0xFF };
        private static readonly byte[] EntranceColor = { 0x20, 0xE0, 0x40, 0xFF };
        private static readonly byte[] SpriteColor = { 0xF0, 0x30, 0x30, 0xFF };

        private readonly Dictionary<(TilesetSlot, Tileset, Tileset), byte[]> atlases = new();
        private readonly List<(byte[] Rgba, int Width, int Height)> thumbnails = new();

        public void RenderArea(LevelArea area, LevelGrid grid, AreaTilesets tilesets, string path) {
            GetBounds(grid, out int minX, out int minY, out int maxX, out int maxY);
            int width = (maxX - minX + 1) * Tile;
            int height = (maxY - minY + 1) * Tile;
            byte[] rgba = new byte[width * height * 4];
            for (int i = 0; i < rgba.Length; i += 4) {
                Sky.CopyTo(rgba, i);
            }

            for (int y = minY; y <= maxY; y++) {
                for (int x = minX; x <= maxX; x++) {
                    int cell = grid.Cells[(y * LevelGrid.Width) + x];
                    if (cell != 0) {
                        TilesetSlot slot = LevelGrid.SplitCell(cell, out int tile);
                        DrawTile(rgba, width, tilesets, slot, tile, (x - minX) * Tile, (y - minY) * Tile);
                    }
                }
            }

            foreach (LevelSprite sprite in area.Sprites) {
                DrawMarker(rgba, width, height, ((sprite.X - minX) * Tile) + 4, ((sprite.Y - minY) * Tile) + 4, 8, SpriteColor);
            }
            foreach (LevelEntrance entrance in area.Entrances) {
                DrawMarker(rgba, width, height, entrance.X - (minX * Tile) + 2, entrance.Y - (minY * Tile) + 2, 12, EntranceColor);
            }

            using FileStream file = File.Create(path);
            PngWriter.Write(file, rgba, width, height);
            thumbnails.Add(MakeThumbnail(rgba, width, height));
        }

        public void WriteContactSheet(string path) {
            int rows = (thumbnails.Count + ContactColumns - 1) / ContactColumns;
            int width = ContactColumns * ContactCellWidth;
            int height = rows * ContactCellHeight;
            byte[] sheet = new byte[width * height * 4];
            for (int i = 3; i < sheet.Length; i += 4) {
                sheet[i] = 255;
            }

            for (int i = 0; i < thumbnails.Count; i++) {
                (byte[] rgba, int thumbWidth, int thumbHeight) = thumbnails[i];
                int originX = (i % ContactColumns) * ContactCellWidth;
                int originY = (i / ContactColumns) * ContactCellHeight;
                for (int y = 0; y < thumbHeight; y++) {
                    Buffer.BlockCopy(rgba, y * thumbWidth * 4, sheet, ((originY + y) * width * 4) + (originX * 4), thumbWidth * 4);
                }
            }

            using FileStream file = File.Create(path);
            PngWriter.Write(file, sheet, width, height);
        }

        private static (byte[] Rgba, int Width, int Height) MakeThumbnail(byte[] rgba, int width, int height) {
            int scale = Math.Max(1, Math.Max((width + ContactCellWidth - 2) / (ContactCellWidth - 1), (height + ContactCellHeight - 2) / (ContactCellHeight - 1)));
            int thumbWidth = width / scale;
            int thumbHeight = height / scale;
            byte[] thumb = new byte[thumbWidth * thumbHeight * 4];
            for (int y = 0; y < thumbHeight; y++) {
                for (int x = 0; x < thumbWidth; x++) {
                    Buffer.BlockCopy(rgba, ((y * scale * width) + (x * scale)) * 4, thumb, ((y * thumbWidth) + x) * 4, 4);
                }
            }
            return (thumb, thumbWidth, thumbHeight);
        }

        private void DrawTile(byte[] rgba, int stride, AreaTilesets tilesets, TilesetSlot slot, int tile, int destX, int destY) {
            Tileset tileset = tilesets[slot];
            (TilesetSlot, Tileset, Tileset) key = (slot, tilesets.Common, tilesets.Level);
            if (!atlases.TryGetValue(key, out byte[]? atlas)) {
                atlas = tileset.RenderAtlas(tilesets, out _, out _);
                atlases[key] = atlas;
            }
            if (tile >= tileset.TileCount) {
                return;
            }

            int atlasStride = Tileset.AtlasColumns * Tile;
            int sourceX = (tile % Tileset.AtlasColumns) * Tile;
            int sourceY = (tile / Tileset.AtlasColumns) * Tile;
            for (int y = 0; y < Tile; y++) {
                for (int x = 0; x < Tile; x++) {
                    int source = (((sourceY + y) * atlasStride) + sourceX + x) * 4;
                    if (atlas[source + 3] != 0) {
                        Buffer.BlockCopy(atlas, source, rgba, (((destY + y) * stride) + destX + x) * 4, 4);
                    }
                }
            }
        }

        private static void DrawMarker(byte[] rgba, int width, int height, int originX, int originY, int size, byte[] color) {
            for (int y = Math.Max(originY, 0); y < Math.Min(originY + size, height); y++) {
                for (int x = Math.Max(originX, 0); x < Math.Min(originX + size, width); x++) {
                    bool border = y == originY || y == originY + size - 1 || x == originX || x == originX + size - 1;
                    if (border) {
                        color.CopyTo(rgba, ((y * width) + x) * 4);
                    }
                }
            }
        }

        private static void GetBounds(LevelGrid grid, out int minX, out int minY, out int maxX, out int maxY) {
            minX = LevelGrid.Width;
            minY = LevelGrid.Height;
            maxX = 0;
            maxY = 0;
            for (int y = 0; y < LevelGrid.Height; y++) {
                for (int x = 0; x < LevelGrid.Width; x++) {
                    if (grid.Cells[(y * LevelGrid.Width) + x] != 0) {
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }
            if (minX > maxX) {
                minX = minY = maxX = maxY = 0;
            }
        }
    }
}
