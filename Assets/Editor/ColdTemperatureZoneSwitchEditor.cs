using System.Collections.Generic;
using LeiHuo.Gameplay.LevelMechanics;
using UnityEditor;
using UnityEngine;

namespace LeiHuo.EditorTools
{
    [CustomEditor(typeof(ColdTemperatureZoneSwitch))]
    public class ColdTemperatureZoneSwitchEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ColdTemperatureZoneSwitch zoneSwitch = (ColdTemperatureZoneSwitch)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Player In Range", zoneSwitch.IsPlayerInRange ? "Yes" : "No");
            EditorGUILayout.LabelField("Active Zones", $"{zoneSwitch.GetActiveZoneCount()} / {zoneSwitch.GetValidZoneCount()}");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Enable Zones"))
                {
                    Undo.RecordObject(zoneSwitch, "Enable Cold Temperature Zones");
                    RecordControlledZones(zoneSwitch, "Enable Cold Temperature Zones");
                    zoneSwitch.SetControlledZonesActive(true);
                    EditorUtility.SetDirty(zoneSwitch);
                    MarkControlledZonesDirty(zoneSwitch);
                }

                if (GUILayout.Button("Disable Zones"))
                {
                    Undo.RecordObject(zoneSwitch, "Disable Cold Temperature Zones");
                    RecordControlledZones(zoneSwitch, "Disable Cold Temperature Zones");
                    zoneSwitch.SetControlledZonesActive(false);
                    EditorUtility.SetDirty(zoneSwitch);
                    MarkControlledZonesDirty(zoneSwitch);
                }
            }

            EditorGUILayout.HelpBox(
                "Scene editing: drag the blue range handle to resize the interaction area. Lines show which cold-temperature zones this switch toggles.",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            ColdTemperatureZoneSwitch zoneSwitch = (ColdTemperatureZoneSwitch)target;
            if (!zoneSwitch.ShowGizmos)
            {
                return;
            }

            DrawRangeHandle(zoneSwitch);
            DrawTargetLines(zoneSwitch);
            DrawStatusLabel(zoneSwitch);
        }

        private void DrawRangeHandle(ColdTemperatureZoneSwitch zoneSwitch)
        {
            Vector3 center = zoneSwitch.GetInteractionWorldCenter();
            Quaternion rotation = zoneSwitch.transform.rotation;
            Handles.color = zoneSwitch.WireColor;

            EditorGUI.BeginChangeCheck();
            if (zoneSwitch.UsesSphereRange())
            {
                float newRadius = Handles.RadiusHandle(rotation, center, zoneSwitch.GetInteractionRadius());
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(zoneSwitch, "Resize Cold Temperature Switch Range");
                    zoneSwitch.SetInteractionRadius(newRadius);
                    EditorUtility.SetDirty(zoneSwitch);
                }
            }
            else
            {
                Vector3 newSize = Handles.ScaleHandle(
                    zoneSwitch.GetInteractionBoxSize(),
                    center,
                    rotation,
                    HandleUtility.GetHandleSize(center));

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(zoneSwitch, "Resize Cold Temperature Switch Range");
                    zoneSwitch.SetInteractionBoxSize(newSize);
                    EditorUtility.SetDirty(zoneSwitch);
                }
            }
        }

        private void DrawTargetLines(ColdTemperatureZoneSwitch zoneSwitch)
        {
            Handles.color = zoneSwitch.TargetLineColor;
            Vector3 from = zoneSwitch.GetInteractionWorldCenter();
            IReadOnlyList<LeiHuo.Gameplay.TemperatureField.ColdTemperatureZone> zones = zoneSwitch.ControlledZones;

            for (int i = 0; i < zones.Count; i++)
            {
                LeiHuo.Gameplay.TemperatureField.ColdTemperatureZone zone = zones[i];
                if (zone == null)
                {
                    continue;
                }

                Handles.DrawAAPolyLine(3f, from, zone.transform.position);
                Handles.Label(
                    Vector3.Lerp(from, zone.transform.position, 0.5f),
                    zone.isActiveAndEnabled ? "On" : "Off");
            }
        }

        private void DrawStatusLabel(ColdTemperatureZoneSwitch zoneSwitch)
        {
            Vector3 center = zoneSwitch.GetInteractionWorldCenter();
            float handleSize = HandleUtility.GetHandleSize(center);

            Handles.Label(
                center + Vector3.up * handleSize * 0.25f,
                $"Cold Temp Switch\nZones {zoneSwitch.GetActiveZoneCount()} / {zoneSwitch.GetValidZoneCount()}\nRange {(zoneSwitch.UsesSphereRange() ? zoneSwitch.GetInteractionRadius().ToString("0.##") : FormatVector(zoneSwitch.GetInteractionBoxSize()))}");
        }

        private string FormatVector(Vector3 value)
        {
            return $"{value.x:0.##}, {value.y:0.##}, {value.z:0.##}";
        }

        private void RecordControlledZones(ColdTemperatureZoneSwitch zoneSwitch, string undoName)
        {
            IReadOnlyList<LeiHuo.Gameplay.TemperatureField.ColdTemperatureZone> zones = zoneSwitch.ControlledZones;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null)
                {
                    Undo.RecordObject(zones[i], undoName);
                }
            }
        }

        private void MarkControlledZonesDirty(ColdTemperatureZoneSwitch zoneSwitch)
        {
            IReadOnlyList<LeiHuo.Gameplay.TemperatureField.ColdTemperatureZone> zones = zoneSwitch.ControlledZones;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null)
                {
                    EditorUtility.SetDirty(zones[i]);
                }
            }
        }
    }
}
