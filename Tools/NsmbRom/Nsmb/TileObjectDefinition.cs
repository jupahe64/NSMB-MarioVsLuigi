using System;
using System.Collections.Generic;

namespace NsmbRom.Nsmb {
    /// <summary>
    /// Tileset object definitions: the recipes that turn one 10-byte level record into a block of Map16 cells.
    /// Cell values are described on <see cref="LevelGrid"/>.
    /// </summary>
    public sealed class TileObjectDefinition {
        public const int EmptyCell = -1;
        public const int KeepCell = -2;

        private const byte LineBreak = 0xFE;
        private const byte ObjectEnd = 0xFF;
        private const byte SlopeControlFlag = 0x80;
        private const byte RepeatX = 1;
        private const byte RepeatY = 2;
        private const byte SlopeGoesLeft = 1;
        private const byte SlopeUpsideDown = 2;
        private const byte ExtendedTileFlag = 0x40;
        private const int ExtendedTileOffset = 768;

        private readonly struct Tile {
            public readonly byte Control;
            public readonly int Cell;

            public Tile(byte control, int cell) {
                Control = control;
                Cell = cell;
            }

            public bool IsSlopeControl => (Control & SlopeControlFlag) != 0;
        }

        private readonly List<List<Tile>> rows;

        private TileObjectDefinition(List<List<Tile>> rows) {
            this.rows = rows;
        }

        public bool IsRenderable {
            get {
                if (rows.Count == 0) {
                    return false;
                }
                foreach (List<Tile> row in rows) {
                    if (row.Count == 0) {
                        return false;
                    }
                }
                return true;
            }
        }

        public static TileObjectDefinition Parse(ReadOnlySpan<byte> data, int offset) {
            List<List<Tile>> rows = new();
            List<Tile> row = new();
            int pos = offset;
            while (pos < data.Length) {
                byte control = data[pos++];
                if (control == ObjectEnd) {
                    break;
                }
                if (control == LineBreak) {
                    rows.Add(row);
                    row = new List<Tile>();
                    continue;
                }
                if ((control & SlopeControlFlag) != 0) {
                    row.Add(new Tile(control, EmptyCell));
                    continue;
                }
                if (pos + 2 > data.Length) {
                    break;
                }

                int low = data[pos++];
                int high = data[pos++];
                int extended = (control & ExtendedTileFlag) != 0 ? ExtendedTileOffset : 0;
                row.Add(new Tile(control, low == 0 && high == 0 ? EmptyCell : LevelGrid.MakeCell(low | (high << 8), extended)));
            }
            return new TileObjectDefinition(rows);
        }

        /// <summary>Lays the object out in a width x height block; returns cell values or Empty/Keep markers.</summary>
        public int[,] Render(int width, int height) {
            int[,] result = new int[width, height];
            if (rows[0][0].IsSlopeControl) {
                RenderSlope(result, width, height);
            } else {
                RenderStandard(result, width, height);
            }
            return result;
        }

        private void RenderStandard(int[,] result, int width, int height) {
            Split(rows, row => (row[0].Control & RepeatY) != 0, out List<List<Tile>> before, out List<List<Tile>> repeating, out List<List<Tile>> after);
            for (int y = 0; y < height; y++) {
                RenderRow(result, Pick(before, repeating, after, y, height), y, width);
            }
        }

        private static void RenderRow(int[,] result, List<Tile> row, int y, int width) {
            Split(row, tile => (tile.Control & RepeatX) != 0, out List<Tile> before, out List<Tile> repeating, out List<Tile> after);
            for (int x = 0; x < width; x++) {
                result[x, y] = Pick(before, repeating, after, x, width).Cell;
            }
        }

        private static void Split<T>(List<T> items, Func<T, bool> repeats, out List<T> before, out List<T> repeating, out List<T> after) {
            before = new List<T>();
            repeating = new List<T>();
            after = new List<T>();
            foreach (T item in items) {
                if (repeats(item)) {
                    repeating.Add(item);
                } else if (repeating.Count > 0) {
                    after.Add(item);
                } else {
                    before.Add(item);
                }
            }
        }

        // Without a repeat section the whole list tiles; otherwise it is head, tiled middle, tail.
        private static T Pick<T>(List<T> before, List<T> repeating, List<T> after, int index, int size) {
            if (repeating.Count == 0) {
                return before[index % before.Count];
            }
            if (index < before.Count) {
                return before[index];
            }
            if (index >= size - after.Count) {
                return after[index - size + after.Count];
            }
            return repeating[(index - before.Count) % repeating.Count];
        }

        private void RenderSlope(int[,] result, int width, int height) {
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    result[x, y] = KeepCell;
                }
            }

            List<Tile?[,]> sections = SlopeSections();
            Tile?[,] main = sections[0];
            Tile?[,]? sub = sections.Count > 1 ? sections[1] : null;
            int mainWidth = main.GetLength(0);
            int mainHeight = main.GetLength(1);

            byte control = rows[0][0].Control;
            bool goesLeft = (control & SlopeGoesLeft) != 0;
            bool upsideDown = (control & SlopeUpsideDown) != 0;

            int stepX = goesLeft ? -mainWidth : mainWidth;
            int stepY = upsideDown ? mainHeight : -mainHeight;
            int blockX = goesLeft ? width - mainWidth : 0;
            int blockY = upsideDown ? 0 : height - mainHeight;

            // The diagonal ends once a whole step lands outside the object.
            bool placed = true;
            while (placed) {
                placed = Place(result, main, blockX, blockY, width, height);
                if (sub != null) {
                    int subX = goesLeft ? blockX + mainWidth - sub.GetLength(0) : blockX;
                    int subY = upsideDown ? blockY - sub.GetLength(1) : blockY + mainHeight;
                    placed |= Place(result, sub, subX, subY, width, height);
                }
                blockX += stepX;
                blockY += stepY;
            }
        }

        private static bool Place(int[,] result, Tile?[,] block, int originX, int originY, int width, int height) {
            bool placed = false;
            for (int x = 0; x < block.GetLength(0); x++) {
                for (int y = 0; y < block.GetLength(1); y++) {
                    int targetX = originX + x;
                    int targetY = originY + y;
                    if (targetX < 0 || targetX >= width || targetY < 0 || targetY >= height) {
                        continue;
                    }
                    placed = true;
                    Tile? tile = block[x, y];
                    if (tile.HasValue) {
                        result[targetX, targetY] = tile.Value.Cell;
                    }
                }
            }
            return placed;
        }

        private List<Tile?[,]> SlopeSections() {
            List<Tile?[,]> sections = new();
            List<List<Tile>>? current = null;
            foreach (List<Tile> row in rows) {
                if (row[0].IsSlopeControl) {
                    if (current != null) {
                        sections.Add(BuildSection(current));
                    }
                    current = new List<List<Tile>>();
                }
                current!.Add(row);
            }
            sections.Add(BuildSection(current!));
            return sections;
        }

        private static Tile?[,] BuildSection(List<List<Tile>> sectionRows) {
            int width = 0;
            foreach (List<Tile> row in sectionRows) {
                width = Math.Max(width, row.FindAll(t => !t.IsSlopeControl).Count);
            }

            Tile?[,] section = new Tile?[width, sectionRows.Count];
            for (int y = 0; y < sectionRows.Count; y++) {
                int x = 0;
                foreach (Tile tile in sectionRows[y]) {
                    if (!tile.IsSlopeControl) {
                        section[x++, y] = tile;
                    }
                }
            }
            return section;
        }
    }
}
