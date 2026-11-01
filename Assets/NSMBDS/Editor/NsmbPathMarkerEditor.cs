using UnityEditor;
using UnityEngine;

namespace NsmbDs.Editor {
    /// <summary>Gives every point of the selected path a translate handle, snapped to whole DS pixels.</summary>
    [CustomEditor(typeof(NsmbPathMarker))]
    public sealed class NsmbPathMarkerEditor : UnityEditor.Editor {
        private void OnSceneGUI() {
            NsmbPathMarker path = (NsmbPathMarker) target;
            for (int i = 0; i < path.points.Length; i++) {
                Handles.Label(path.points[i], i.ToString());

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(path.points[i], Quaternion.identity);
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(path, "Move path point");
                    path.points[i] = SnapToPixel(moved);
                    EditorUtility.SetDirty(path);
                }
            }
        }

        private static Vector3 SnapToPixel(Vector3 position) {
            float pixels = NsmbUnits.PixelsPerUnit;
            return new Vector3(Mathf.Round(position.x * pixels) / pixels, Mathf.Round(position.y * pixels) / pixels, 0f);
        }
    }
}
