using UnityEngine;

namespace NsmbDs {
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
