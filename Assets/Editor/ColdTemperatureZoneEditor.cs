using LeiHuo.Gameplay.TemperatureField;
using UnityEditor;
using UnityEngine;

namespace LeiHuo.EditorTools
{
    [CustomEditor(typeof(ColdTemperatureZone))]
    public class ColdTemperatureZoneEditor : Editor
    {
        private SerializedProperty shapeProperty;
        private SerializedProperty centerOffsetProperty;
        private SerializedProperty radiusProperty;
        private SerializedProperty boxSizeProperty;

        private void OnEnable()
        {
            shapeProperty = serializedObject.FindProperty("shape");
            centerOffsetProperty = serializedObject.FindProperty("centerOffset");
            radiusProperty = serializedObject.FindProperty("radius");
            boxSizeProperty = serializedObject.FindProperty("boxSize");
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.HelpBox(
                "Scene editing: drag the blue handle to resize the cold area. Water vapor freezes automatically, ice does not melt, freezable objects freeze, and uncontrolled machines stop while inside.",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            serializedObject.Update();

            ColdTemperatureZone zone = (ColdTemperatureZone)target;
            Transform zoneTransform = zone.transform;
            Vector3 worldCenter = zoneTransform.TransformPoint(centerOffsetProperty.vector3Value);
            Quaternion rotation = zoneTransform.rotation;

            Handles.color = new Color(0.2f, 0.65f, 1f, 0.95f);
            EditorGUI.BeginChangeCheck();

            if (shapeProperty.enumValueIndex == (int)ColdTemperatureZone.ZoneShape.Sphere)
            {
                float newRadius = Handles.RadiusHandle(rotation, worldCenter, radiusProperty.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(zone, "Resize Cold Temperature Zone");
                    radiusProperty.floatValue = Mathf.Max(0.01f, newRadius);
                }
            }
            else
            {
                Vector3 newSize = Handles.ScaleHandle(
                    boxSizeProperty.vector3Value,
                    worldCenter,
                    rotation,
                    HandleUtility.GetHandleSize(worldCenter));

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(zone, "Resize Cold Temperature Zone");
                    boxSizeProperty.vector3Value = new Vector3(
                        Mathf.Max(0.01f, newSize.x),
                        Mathf.Max(0.01f, newSize.y),
                        Mathf.Max(0.01f, newSize.z));
                }
            }

            Handles.Label(
                worldCenter + Vector3.up * HandleUtility.GetHandleSize(worldCenter) * 0.2f,
                shapeProperty.enumValueIndex == (int)ColdTemperatureZone.ZoneShape.Sphere
                    ? $"Cold Zone\nRadius {radiusProperty.floatValue:0.##}"
                    : $"Cold Zone\nSize {FormatVector(boxSizeProperty.vector3Value)}");

            serializedObject.ApplyModifiedProperties();
        }

        private string FormatVector(Vector3 value)
        {
            return $"{value.x:0.##}, {value.y:0.##}, {value.z:0.##}";
        }
    }
}
