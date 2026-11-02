using System;
using UnityEngine;

namespace NsmbDs {
    /// <summary>A power-up rising out of a block, then walking until the player touches it.</summary>
    public sealed class NsmbItem : MonoBehaviour {
        private const int RiseFrames = 51;
        private const float RiseSpeed = 0.3125f;
        private const float WalkSpeed = 1f;
        private const float Gravity = 0.25f;
        private const float MaxFall = 4f;
        private const float HalfSize = 8f;
        private const float StepSeconds = NsmbUnits.StepSeconds;
        private const int ItemSortingOrder = 15;
        private const float LowestY = 4096f;

        private NsmbItemKind kind;
        private NsmbPlayerMotor motor;
        private NsmbPlayer owner;
        private NsmbTileSource tiles;
        private Action<NsmbItemKind> collected;
        private NsmbPlayerTuning tuning;
        private float x;
        private float y;
        private float speedX;
        private float speedY;
        private int rise;
        private float pending;

        public static NsmbItem Spawn(NsmbItemKind kind, int column, int row, NsmbPlayer owner, NsmbItemAssets assets, Action<NsmbItemKind> collected) {
            NsmbPlayerMotor motor = owner.Motor;
            NsmbPlayerTuning tuning = owner.tuning;
            GameObject body = new("Item " + kind);
            SpriteRenderer view = body.AddComponent<SpriteRenderer>();
            view.sprite = assets != null ? assets.SpriteOf(kind) : null;
            view.sortingOrder = ItemSortingOrder;
            NsmbItem item = body.AddComponent<NsmbItem>();
            item.kind = kind;
            item.motor = motor;
            item.owner = owner;
            item.tuning = tuning;
            item.tiles = new NsmbTileReader(motor);
            item.collected = collected;
            item.x = (column * NsmbTileSource.TileSize) + HalfSize;
            item.y = (row * NsmbTileSource.TileSize) + NsmbTileSource.TileSize;
            item.rise = RiseFrames;
            item.speedX = kind == NsmbItemKind.FireFlower ? 0f : (motor.X > item.x ? -WalkSpeed : WalkSpeed);
            item.Place();
            return item;
        }

        private sealed class NsmbTileReader : NsmbTileSource {
            private readonly NsmbPlayerMotor owner;

            public NsmbTileReader(NsmbPlayerMotor owner) {
                this.owner = owner;
            }

            public override NsmbTileBehavior GetTile(int column, int row) {
                return owner.Tiles.GetTile(column, row);
            }
        }

        private void Update() {
            if (motor == null || owner == null || owner.Motor != motor) {
                Destroy(gameObject);
                return;
            }

            pending += Time.deltaTime;
            while (pending >= StepSeconds) {
                pending -= StepSeconds;
                Step();
            }
            Place();
        }

        private void Step() {
            if (rise > 0) {
                rise--;
                y -= RiseSpeed;
            } else if (kind != NsmbItemKind.FireFlower) {
                Walk();
            }

            float height = motor.BodyHeight;
            if (Mathf.Abs(motor.X - x) < tuning.halfWidth + HalfSize && motor.Y > y - (HalfSize * 2f) && motor.Y - height < y) {
                collected(kind);
                Destroy(gameObject);
            } else if (y > LowestY) {
                Destroy(gameObject);
            }
        }

        private void Walk() {
            x += speedX;
            float edge = x + (speedX > 0f ? HalfSize : -HalfSize);
            if (tiles.GetTileAt(edge, y - HalfSize).IsFullSolid) {
                x -= speedX;
                speedX = -speedX;
            }

            speedY = Mathf.Min(MaxFall, speedY + Gravity);
            float next = y + speedY;
            int row = Mathf.FloorToInt(next / NsmbTileSource.TileSize);
            NsmbTileBehavior below = tiles.GetTileAt(x, next);
            if (speedY > 0f && (below.IsFullSolid || below.IsSolidOnTop)) {
                y = row * (float) NsmbTileSource.TileSize;
                speedY = 0f;
            } else {
                y = next;
            }
        }

        private void Place() {
            transform.position = new Vector3(x / NsmbUnits.PixelsPerUnit, (-y + HalfSize) / NsmbUnits.PixelsPerUnit, -0.5f);
        }
    }
}
