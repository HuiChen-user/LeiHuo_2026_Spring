using LeiHuo.Gameplay.TemperatureField;
using UnityEditor;
using UnityEngine;

namespace LeiHuo.EditorTools
{
    [CustomEditor(typeof(HighTemperatureZone))]
    public class HighTemperatureZoneEditor : Editor
    {
        private SerializedProperty shapeProperty;
        private SerializedProperty centerOffsetProperty;
        private SerializedProperty radiusProperty;
        private SerializedProperty boxSizeProperty;
        private SerializedProperty slowSpeedMultiplierProperty;
        private SerializedProperty slowDurationProperty;
        private SerializedProperty enhancedStopDurationProperty;

        private void OnEnable()
        {
            shapeProperty = serializedObject.FindProperty("shape");
            centerOffsetProperty = serializedObject.FindProperty("centerOffset");
            radiusProperty = serializedObject.FindProperty("radius");
            boxSizeProperty = serializedObject.FindProperty("boxSize");
            slowSpeedMultiplierProperty = serializedObject.FindProperty("uncontrolledSlowSpeedMultiplier");
            slowDurationProperty = serializedObject.FindProperty("uncontrolledSlowDurationAfterLeavingField");
            enhancedStopDurationProperty = serializedObject.FindProperty("enhancedStopDurationAfterLeavingField");
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.HelpBox(
                "Scene editing: drag the red handle to resize the high-temperature area. Runtime behavior uses the player's position when the temperature field is released.",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            serializedObject.Update();

            HighTemperatureZone zone = (HighTemperatureZone)target;
            Transform zoneTransform = zone.transform;
            Vector3 localCenter = centerOffsetProperty.vector3Value;
            Vector3 worldCenter = zoneTransform.TransformPoint(localCenter);
            Quaternion rotation = zoneTransform.rotation;

            Handles.color = new Color(1f, 0.2f, 0.08f, 0.9f);
            EditorGUI.BeginChangeCheck();

            if (shapeProperty.enumValueIndex == (int)HighTemperatureZone.ZoneShape.Sphere)
            {
                float newRadius = Handles.RadiusHandle(rotation, worldCenter, radiusProperty.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(zone, "Resize High Temperature Zone");
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
                    Undo.RecordObject(zone, "Resize High Temperature Zone");
                    boxSizeProperty.vector3Value = new Vector3(
                        Mathf.Max(0.01f, newSize.x),
                        Mathf.Max(0.01f, newSize.y),
                        Mathf.Max(0.01f, newSize.z));
                }
            }

            Handles.Label(
                worldCenter + Vector3.up * HandleUtility.GetHandleSize(worldCenter) * 0.2f,
                $"High Temp\nSlow x{slowSpeedMultiplierProperty.floatValue:0.##} / {slowDurationProperty.floatValue:0.##}s\nEnhanced Stop {enhancedStopDurationProperty.floatValue:0.##}s");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
