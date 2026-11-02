using UnityEngine;

namespace NsmbDs {
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
}
