using UnityEngine;

namespace NsmbDs {
    /// <summary>Base of all level actors: 60 Hz stepping, tile movement, and the shared rules for touching the player.</summary>
    public abstract class NsmbActor : MonoBehaviour {
        private const float StepSeconds = NsmbUnits.StepSeconds;
        private const float Gravity = 0.25f;
        private const float MaxFall = 4f;
        private const float Depth = -2f;
        private const int SortingOrder = 12;
        private const float StompBounceSpeed = 7.265625f;

        protected NsmbStage Stage { get; private set; }
        protected NsmbPlayerMotor Player { get; private set; }
        protected NsmbTileSource Tiles { get; private set; }
        protected SpriteRenderer View { get; private set; }

        /// <summary>Feet position in DS pixels, y growing downwards.</summary>
        public float X { get; protected set; }
        public float Y { get; protected set; }
        protected float SpeedX;
        protected float SpeedY;
        protected bool Grounded;
        public bool Defeated { get; protected set; }

        protected virtual float HalfWidth => 8f;
        protected virtual float Height => 16f;

        /// <summary>Marker that spawned this actor, so it can spawn again once off screen.</summary>
        public NsmbSpriteMarker Marker { get; private set; }

        private float pending;

        public void Setup(NsmbStage stage, NsmbSpriteMarker marker, float x, float y) {
            Stage = stage;
            Player = stage.Motor;
            Tiles = stage.Motor.Tiles;
            Marker = marker;
            X = x;
            Y = y;
            View = gameObject.AddComponent<SpriteRenderer>();
            View.sortingOrder = SortingOrder;
            OnSpawn();
            Place();
        }

        protected virtual void OnSpawn() {
        }

        protected abstract void StepActor();

        /// <summary>The player landed on this actor.</summary>
        protected virtual void OnStomped() {
            Defeated = true;
        }

        private void Update() {
            if (Stage == null || Stage.CurrentMotor != Player) {
                Remove();
                return;
            }

            pending += Time.deltaTime;
            while (pending >= StepSeconds) {
                pending -= StepSeconds;
                StepActor();
                if (!Defeated) {
                    TouchPlayer();
                }
            }
            Place();
        }

        private void TouchPlayer() {
            float height = Player.BodyHeight;
            bool overlaps = Mathf.Abs(Player.X - X) < HalfWidth + 6f && Player.Y > Y - Height && Player.Y - height < Y;
            if (!overlaps) {
                return;
            }

            if (Stage.HasStar) {
                OnStomped();
            } else if (Player.VelocityY < 0f && Player.Y < Y - (Height / 2f) + 4f) {
                Player.VelocityY = StompBounceSpeed;
                OnStomped();
            } else {
                OnPlayerTouch();
            }
        }

        /// <summary>The player walked into this actor.</summary>
        protected virtual void OnPlayerTouch() {
            Stage.HurtPlayer();
        }

        /// <summary>Hit by a moving shell or similar.</summary>
        public virtual void DefeatByActor() {
            Defeated = true;
            Remove();
        }

        public bool Touches(NsmbActor other) {
            return Mathf.Abs(other.X - X) < HalfWidth + other.HalfWidth && Mathf.Abs(other.Y - Y) < Height;
        }

        /// <summary>Moves with gravity and tile collision; walls reverse the actor.</summary>
        protected void WalkOnTiles() {
            X += SpeedX;
            float edge = X + (SpeedX > 0f ? HalfWidth : -HalfWidth);
            if (SpeedX != 0f && Tiles.GetTileAt(edge, Y - (Height / 2f)).IsFullSolid) {
                X -= SpeedX;
                SpeedX = -SpeedX;
            }

            SpeedY = Mathf.Min(MaxFall, SpeedY + Gravity);
            float next = Y + SpeedY;
            NsmbTileBehavior below = Tiles.GetTileAt(X, next);
            int row = Mathf.FloorToInt(next / NsmbTileSource.TileSize);
            if (SpeedY > 0f && (below.IsFullSolid || below.IsSolidOnTop) && Y <= row * (float) NsmbTileSource.TileSize) {
                Y = row * (float) NsmbTileSource.TileSize;
                SpeedY = 0f;
                Grounded = true;
            } else {
                Y = next;
                Grounded = false;
            }
        }

        private void Place() {
            transform.position = new Vector3(X / NsmbUnits.PixelsPerUnit, -Y / NsmbUnits.PixelsPerUnit, Depth);
        }

        protected void Remove() {
            Destroy(gameObject);
        }
    }
}
