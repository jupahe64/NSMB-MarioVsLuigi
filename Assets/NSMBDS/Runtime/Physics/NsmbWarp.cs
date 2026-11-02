using System;

namespace NsmbDs {
    public enum NsmbEntranceKind { Other, Door, PipeUp, PipeDown, PipeLeft, PipeRight }

    /// <summary>Where pipes and doors sit around their entrance. Entrances mark a pipe's mouth tile: its left one, or for side pipes its top one.</summary>
    public readonly struct NsmbWarp {
        public const int ExitOnlySetting = 0x80;
        public const int ConnectedSetting = 0x08;
        private const float PipeSpan = 32f;
        private const float MiniPipeSpan = 16f;
        private const float DoorWidth = 32f;
        private const float DoorHeight = 48f;
        private const float Reach = 12f;
        private const float TileSize = NsmbTileSource.TileSize;
        private const int FirstMiniType = 16;

        public readonly NsmbEntranceKind Kind;
        private readonly float x;
        private readonly float y;
        private readonly float span;

        public NsmbWarp(int type, float x, float y) {
            Kind = KindOf(type);
            this.x = x;
            this.y = y;
            span = type >= FirstMiniType ? MiniPipeSpan : PipeSpan;
        }

        public bool IsMini => span == MiniPipeSpan;

        /// <summary>Unit step of the slide into the pipe.</summary>
        public (float X, float Y) EnterDirection => Kind switch {
            NsmbEntranceKind.PipeUp => (0f, 1f),
            NsmbEntranceKind.PipeDown => (0f, -1f),
            NsmbEntranceKind.PipeLeft => (1f, 0f),
            NsmbEntranceKind.PipeRight => (-1f, 0f),
            _ => (0f, 0f),
        };

        public static NsmbEntranceKind KindOf(int type) {
            switch (type) {
            case 2:
                return NsmbEntranceKind.Door;
            case 3: case 16: case 22:
                return NsmbEntranceKind.PipeUp;
            case 4: case 17: case 23:
                return NsmbEntranceKind.PipeDown;
            case 5: case 18: case 24:
                return NsmbEntranceKind.PipeLeft;
            case 6: case 19: case 25:
                return NsmbEntranceKind.PipeRight;
            default:
                return NsmbEntranceKind.Other;
            }
        }

        public bool CanEnter(NsmbPlayerMotor motor, in NsmbPlayerInput input, float bodyHeight) {
            float feetOfSidePipe = y + span;
            switch (Kind) {
            case NsmbEntranceKind.Door:
                return input.Up && motor.Grounded && motor.X >= x && motor.X <= x + DoorWidth && motor.Y > y && motor.Y <= y + DoorHeight;
            case NsmbEntranceKind.PipeUp:
                return input.Down && motor.Grounded && motor.X >= x && motor.X <= x + span && Math.Abs(motor.Y - y) < Reach;
            case NsmbEntranceKind.PipeDown:
                return input.Up && motor.X >= x && motor.X <= x + span && Math.Abs(motor.Y - bodyHeight - (y + TileSize)) < Reach;
            case NsmbEntranceKind.PipeLeft:
                return input.Right && motor.Grounded && Math.Abs(motor.X - x) < Reach && Math.Abs(motor.Y - feetOfSidePipe) < Reach;
            case NsmbEntranceKind.PipeRight:
                return input.Left && motor.Grounded && Math.Abs(motor.X - (x + TileSize)) < Reach && Math.Abs(motor.Y - feetOfSidePipe) < Reach;
            default:
                return false;
            }
        }

        /// <summary>Lines Mario up with the pipe's middle before he slides in.</summary>
        public (float X, float Y) EnterPosition(float motorX, float motorY) {
            return Kind switch {
                NsmbEntranceKind.PipeUp or NsmbEntranceKind.PipeDown => (x + (span / 2f), motorY),
                NsmbEntranceKind.PipeLeft or NsmbEntranceKind.PipeRight => (motorX, y + span),
                _ => (motorX, motorY),
            };
        }

        /// <summary>Where Mario ends up after leaving through this entrance: just clear of the pipe, or at a door.</summary>
        public (float X, float Y) ExitPosition(NsmbTileSource tiles, float halfWidth, float bodyHeight) {
            (float stepX, float stepY) = EnterDirection;
            float mouthX = x;
            float mouthY = y;

            // Some entrances sit deeper inside their pipe; follow it out to the mouth.
            while (tiles.GetTileAt(mouthX - (stepX * TileSize) + 1f, mouthY - (stepY * TileSize) + 1f).IsPipe && (stepX != 0f || stepY != 0f)) {
                mouthX -= stepX * TileSize;
                mouthY -= stepY * TileSize;
            }

            switch (Kind) {
            case NsmbEntranceKind.Door:
                return (x + (DoorWidth / 2f), y + TileSize);
            case NsmbEntranceKind.PipeUp:
                return (mouthX + (span / 2f), mouthY);
            case NsmbEntranceKind.PipeDown:
                return (mouthX + (span / 2f), mouthY + TileSize + bodyHeight + 1f);
            case NsmbEntranceKind.PipeLeft:
                return (mouthX - halfWidth - 1f, mouthY + span);
            case NsmbEntranceKind.PipeRight:
                return (mouthX + TileSize + halfWidth + 1f, mouthY + span);
            default:
                return (x + (TileSize / 2f), y + TileSize);
            }
        }
    }
}
