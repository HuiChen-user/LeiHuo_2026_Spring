using LeiHuo.Gameplay.LevelMechanics;
using UnityEditor;
using UnityEngine;

namespace LeiHuo.EditorTools
{
    [CustomEditor(typeof(UncontrolledMachineRouteMover))]
    public class UncontrolledMachineRouteMoverEditor : Editor
    {
        private SerializedProperty routePointsProperty;

        private void OnEnable()
        {
            routePointsProperty = serializedObject.FindProperty("routePoints");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Route Point"))
                {
                    AddRoutePoint();
                }

                using (new EditorGUI.DisabledScope(routePointsProperty.arraySize <= 2))
                {
                    if (GUILayout.Button("Remove Last Point"))
                    {
                        routePointsProperty.DeleteArrayElementAtIndex(routePointsProperty.arraySize - 1);
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "Scene editing: select the machine, drag numbered route handles, Ctrl-click a segment handle to insert a point, Alt-click a point handle to remove it.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            UncontrolledMachineRouteMover mover = (UncontrolledMachineRouteMover)target;
            if (!mover.ShowRouteGizmos || mover.RoutePoints == null || mover.RoutePoints.Count == 0)
            {
                return;
            }

            serializedObject.Update();

            Handles.color = mover.RouteColor;
            DrawRouteLines(mover);
            DrawPointHandles(mover);
            DrawSegmentInsertHandles(mover);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRouteLines(UncontrolledMachineRouteMover mover)
        {
            for (int i = 0; i < mover.RoutePoints.Count - 1; i++)
            {
                Handles.DrawAAPolyLine(4f, mover.GetWorldPoint(i), mover.GetWorldPoint(i + 1));
            }

            if (mover.CurrentRouteMode == UncontrolledMachineRouteMover.RouteMode.Loop && mover.RoutePoints.Count > 2)
            {
                Handles.DrawAAPolyLine(4f, mover.GetWorldPoint(mover.RoutePoints.Count - 1), mover.GetWorldPoint(0));
            }
        }

        private void DrawPointHandles(UncontrolledMachineRouteMover mover)
        {
            for (int i = 0; i < mover.RoutePoints.Count; i++)
            {
                Vector3 worldPoint = mover.GetWorldPoint(i);
                float handleSize = HandleUtility.GetHandleSize(worldPoint) * 0.12f;

                EditorGUI.BeginChangeCheck();
                Vector3 newWorldPoint = Handles.PositionHandle(worldPoint, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(mover, "Move Machine Route Point");
                    mover.SetWorldPoint(i, newWorldPoint);
                    EditorUtility.SetDirty(mover);
                }

                Handles.Label(worldPoint + Vector3.up * handleSize, $"P{i}");

                if (Event.current.alt && Handles.Button(worldPoint, Quaternion.identity, handleSize, handleSize, Handles.SphereHandleCap))
                {
                    Undo.RecordObject(mover, "Remove Machine Route Point");
                    mover.RemovePointAt(i);
                    EditorUtility.SetDirty(mover);
                    Event.current.Use();
                    break;
                }
            }
        }

        private void DrawSegmentInsertHandles(UncontrolledMachineRouteMover mover)
        {
            int segmentCount = mover.RoutePoints.Count - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                DrawInsertHandle(mover, i, mover.GetWorldPoint(i), mover.GetWorldPoint(i + 1));
            }

            if (mover.CurrentRouteMode == UncontrolledMachineRouteMover.RouteMode.Loop && mover.RoutePoints.Count > 2)
            {
                DrawInsertHandle(mover, mover.RoutePoints.Count - 1, mover.GetWorldPoint(mover.RoutePoints.Count - 1), mover.GetWorldPoint(0));
            }
        }

        private void DrawInsertHandle(UncontrolledMachineRouteMover mover, int afterIndex, Vector3 from, Vector3 to)
        {
            Vector3 midpoint = Vector3.Lerp(from, to, 0.5f);
            float handleSize = HandleUtility.GetHandleSize(midpoint) * 0.08f;

            if (!Event.current.control)
            {
                return;
            }

            if (Handles.Button(midpoint, Quaternion.identity, handleSize, handleSize, Handles.CircleHandleCap))
            {
                Undo.RecordObject(mover, "Insert Machine Route Point");
                mover.InsertWorldPointAfter(afterIndex, midpoint);
                EditorUtility.SetDirty(mover);
                Event.current.Use();
            }
        }

        private void AddRoutePoint()
        {
            UncontrolledMachineRouteMover mover = (UncontrolledMachineRouteMover)target;
            Undo.RecordObject(mover, "Add Machine Route Point");

            Vector3 newPoint;
            if (mover.RoutePoints != null && mover.RoutePoints.Count > 0)
            {
                Vector3 lastPoint = mover.GetWorldPoint(mover.RoutePoints.Count - 1);
                newPoint = lastPoint + mover.transform.forward * 2f;
            }
            else
            {
                newPoint = mover.transform.position;
            }

            mover.InsertWorldPointAfter(mover.RoutePoints == null ? -1 : mover.RoutePoints.Count - 1, newPoint);
            EditorUtility.SetDirty(mover);
        }
    }
}
