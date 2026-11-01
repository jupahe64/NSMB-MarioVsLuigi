using UnityEngine;

namespace NsmbDs {
    public sealed class NsmbEntranceMarker : MonoBehaviour {
        public int number;
        public int type;
        public int destArea;
        public int destEntrance;
        public int connectedPipe;
        public int settings;
        public int entryView;
        public Vector2Int cameraPixels;

        private void OnDrawGizmos() {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + new Vector3(0.25f, -0.25f), new Vector3(0.5f, 0.5f));
        }
    }

    public sealed class NsmbSpriteMarker : MonoBehaviour {
        public int type;
        public int[] data;

        private void OnDrawGizmos() {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + new Vector3(0.25f, -0.25f), new Vector3(0.4f, 0.4f));
        }
    }

    /// <summary>A view or zone rectangle; the transform sits on its top-left corner.</summary>
    public sealed class NsmbRegionMarker : MonoBehaviour {
        public int number;
        public bool isZone;
        public Vector2 size;
        public int cameraId;
        public int music;
        public int lighting;

        public Rect WorldRect => new(transform.position.x, transform.position.y - size.y, size.x, size.y);

        private void OnDrawGizmos() {
            Gizmos.color = isZone ? Color.yellow : Color.cyan;
            Gizmos.DrawWireCube(transform.position + new Vector3(size.x / 2f, -size.y / 2f), size);
        }
    }

    public sealed class NsmbPathMarker : MonoBehaviour {
        private const float PointRadius = 0.125f;

        public int id;
        public bool isProgressPath;
        public Vector3[] points;

        private void OnDrawGizmos() {
            Gizmos.color = isProgressPath ? Color.white : Color.magenta;
            for (int i = 0; i < points.Length; i++) {
                Gizmos.DrawWireSphere(points[i], PointRadius);
                if (i > 0) {
                    Gizmos.DrawLine(points[i - 1], points[i]);
                }
            }
        }
    }
}
