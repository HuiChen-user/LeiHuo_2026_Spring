using LeiHuo.Gameplay.LevelMechanics;
using UnityEditor;
using UnityEngine;

namespace LeiHuo.EditorTools
{
    [CustomEditor(typeof(PressurePlatePlatformSwitch))]
    public class PressurePlatePlatformSwitchEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PressurePlatePlatformSwitch pressurePlate = (PressurePlatePlatformSwitch)target;

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(pressurePlate.Platform == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Capture A From Platform"))
                    {
                        Undo.RecordObject(pressurePlate, "Capture Pressure Plate Platform A Point");
                        pressurePlate.CaptureInactiveFromPlatform();
                        EditorUtility.SetDirty(pressurePlate);
                    }

                    if (GUILayout.Button("Capture B From Platform"))
                    {
                        Undo.RecordObject(pressurePlate, "Capture Pressure Plate Platform B Point");
                        pressurePlate.CaptureActiveFromPlatform();
                        EditorUtility.SetDirty(pressurePlate);
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "Scene editing: drag the yellow A handle for the released platform position, and the green B handle for the pressed platform position.",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            PressurePlatePlatformSwitch pressurePlate = (PressurePlatePlatformSwitch)target;
            if (!pressurePlate.ShowGizmos)
            {
                return;
            }

            DrawRouteLine(pressurePlate);
            DrawPointHandle(
                pressurePlate,
                pressurePlate.GetInactiveWorldPoint(),
                pressurePlate.InactiveColor,
                "A",
                "Move Pressure Plate Platform A Point",
                pressurePlate.SetInactiveWorldPoint);
            DrawPointHandle(
                pressurePlate,
                pressurePlate.GetActiveWorldPoint(),
                pressurePlate.ActiveColor,
                "B",
                "Move Pressure Plate Platform B Point",
                pressurePlate.SetActiveWorldPoint);
        }

        private void DrawRouteLine(PressurePlatePlatformSwitch pressurePlate)
        {
            Handles.color = pressurePlate.LineColor;
            Handles.DrawAAPolyLine(
                4f,
                pressurePlate.GetInactiveWorldPoint(),
                pressurePlate.GetActiveWorldPoint());
        }

        private void DrawPointHandle(
            PressurePlatePlatformSwitch pressurePlate,
            Vector3 worldPoint,
            Color color,
            string label,
            string undoName,
            System.Action<Vector3> setter)
        {
            Handles.color = color;
            float handleSize = HandleUtility.GetHandleSize(worldPoint) * 0.15f;

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPoint = Handles.PositionHandle(worldPoint, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pressurePlate, undoName);
                setter(newWorldPoint);
                EditorUtility.SetDirty(pressurePlate);
            }

            Handles.SphereHandleCap(0, worldPoint, Quaternion.identity, handleSize, EventType.Repaint);
            Handles.Label(worldPoint + Vector3.up * handleSize, label);
        }
    }
}
