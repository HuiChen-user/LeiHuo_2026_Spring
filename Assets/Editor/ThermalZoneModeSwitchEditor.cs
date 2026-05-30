using System.Collections.Generic;
using LeiHuo.Gameplay.LevelMechanics;
using UnityEditor;
using UnityEngine;

namespace LeiHuo.EditorTools
{
    [CustomEditor(typeof(ThermalZoneModeSwitch))]
    public class ThermalZoneModeSwitchEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ThermalZoneModeSwitch modeSwitch = (ThermalZoneModeSwitch)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Player In Range", modeSwitch.IsPlayerInRange ? "Yes" : "No");
            EditorGUILayout.LabelField("Mode", modeSwitch.IsColdModeActive ? "Cold" : modeSwitch.IsHighModeActive ? "High" : "Mixed / Off");
            EditorGUILayout.LabelField("High Zones", $"{modeSwitch.GetActiveHighZoneCount()} / {modeSwitch.GetValidHighZoneCount()}");
            EditorGUILayout.LabelField("Cold Zones", $"{modeSwitch.GetActiveColdZoneCount()} / {modeSwitch.GetValidColdZoneCount()}");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set High"))
                {
                    RecordTargets(modeSwitch, "Set High Temperature Mode");
                    modeSwitch.SetHighTemperatureMode();
                    MarkTargetsDirty(modeSwitch);
                }

                if (GUILayout.Button("Set Cold"))
                {
                    RecordTargets(modeSwitch, "Set Cold Temperature Mode");
                    modeSwitch.SetColdTemperatureMode();
                    MarkTargetsDirty(modeSwitch);
                }
            }

            EditorGUILayout.HelpBox(
                "Scene editing: drag the range handle to resize the interaction area. Orange lines point to high-temperature zones, blue lines point to cold-temperature zones.",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            ThermalZoneModeSwitch modeSwitch = (ThermalZoneModeSwitch)target;
            if (!modeSwitch.ShowGizmos)
            {
                return;
            }

            DrawRangeHandle(modeSwitch);
            DrawTargetLines(modeSwitch);
            DrawStatusLabel(modeSwitch);
        }

        private void DrawRangeHandle(ThermalZoneModeSwitch modeSwitch)
        {
            Vector3 center = modeSwitch.GetInteractionWorldCenter();
            Quaternion rotation = modeSwitch.transform.rotation;
            Handles.color = modeSwitch.WireColor;

            EditorGUI.BeginChangeCheck();
            if (modeSwitch.UsesSphereRange())
            {
                float newRadius = Handles.RadiusHandle(rotation, center, modeSwitch.GetInteractionRadius());
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(modeSwitch, "Resize Thermal Mode Switch Range");
                    modeSwitch.SetInteractionRadius(newRadius);
                    EditorUtility.SetDirty(modeSwitch);
                }
            }
            else
            {
                Vector3 newSize = Handles.ScaleHandle(
                    modeSwitch.GetInteractionBoxSize(),
                    center,
                    rotation,
                    HandleUtility.GetHandleSize(center));

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(modeSwitch, "Resize Thermal Mode Switch Range");
                    modeSwitch.SetInteractionBoxSize(newSize);
                    EditorUtility.SetDirty(modeSwitch);
                }
            }
        }

        private void DrawTargetLines(ThermalZoneModeSwitch modeSwitch)
        {
            Vector3 from = modeSwitch.GetInteractionWorldCenter();

            Handles.color = modeSwitch.HighTargetLineColor;
            IReadOnlyList<LeiHuo.Gameplay.TemperatureField.HighTemperatureZone> highZones = modeSwitch.HighTemperatureZones;
            for (int i = 0; i < highZones.Count; i++)
            {
                LeiHuo.Gameplay.TemperatureField.HighTemperatureZone zone = highZones[i];
                if (zone == null)
                {
                    continue;
                }

                Handles.DrawAAPolyLine(3f, from, zone.transform.position);
                Handles.Label(Vector3.Lerp(from, zone.transform.position, 0.5f), zone.isActiveAndEnabled ? "High On" : "High Off");
            }

            Handles.color = modeSwitch.ColdTargetLineColor;
            IReadOnlyList<LeiHuo.Gameplay.TemperatureField.ColdTemperatureZone> coldZones = modeSwitch.ColdTemperatureZones;
            for (int i = 0; i < coldZones.Count; i++)
            {
                LeiHuo.Gameplay.TemperatureField.ColdTemperatureZone zone = coldZones[i];
                if (zone == null)
                {
                    continue;
                }

                Handles.DrawAAPolyLine(3f, from, zone.transform.position);
                Handles.Label(Vector3.Lerp(from, zone.transform.position, 0.5f), zone.isActiveAndEnabled ? "Cold On" : "Cold Off");
            }
        }

        private void DrawStatusLabel(ThermalZoneModeSwitch modeSwitch)
        {
            Vector3 center = modeSwitch.GetInteractionWorldCenter();
            float handleSize = HandleUtility.GetHandleSize(center);
            string mode = modeSwitch.IsColdModeActive ? "Cold" : modeSwitch.IsHighModeActive ? "High" : "Mixed / Off";

            Handles.Label(
                center + Vector3.up * handleSize * 0.25f,
                $"Thermal Mode Switch\nMode {mode}\nHigh {modeSwitch.GetActiveHighZoneCount()} / {modeSwitch.GetValidHighZoneCount()}  Cold {modeSwitch.GetActiveColdZoneCount()} / {modeSwitch.GetValidColdZoneCount()}\nRange {(modeSwitch.UsesSphereRange() ? modeSwitch.GetInteractionRadius().ToString("0.##") : FormatVector(modeSwitch.GetInteractionBoxSize()))}");
        }

        private string FormatVector(Vector3 value)
        {
            return $"{value.x:0.##}, {value.y:0.##}, {value.z:0.##}";
        }

        private void RecordTargets(ThermalZoneModeSwitch modeSwitch, string undoName)
        {
            Undo.RecordObject(modeSwitch, undoName);

            IReadOnlyList<LeiHuo.Gameplay.TemperatureField.HighTemperatureZone> highZones = modeSwitch.HighTemperatureZones;
            for (int i = 0; i < highZones.Count; i++)
            {
                if (highZones[i] != null)
                {
                    Undo.RecordObject(highZones[i], undoName);
                }
            }

            IReadOnlyList<LeiHuo.Gameplay.TemperatureField.ColdTemperatureZone> coldZones = modeSwitch.ColdTemperatureZones;
            for (int i = 0; i < coldZones.Count; i++)
            {
                if (coldZones[i] != null)
                {
                    Undo.RecordObject(coldZones[i], undoName);
                }
            }
        }

        private void MarkTargetsDirty(ThermalZoneModeSwitch modeSwitch)
        {
            EditorUtility.SetDirty(modeSwitch);

            IReadOnlyList<LeiHuo.Gameplay.TemperatureField.HighTemperatureZone> highZones = modeSwitch.HighTemperatureZones;
            for (int i = 0; i < highZones.Count; i++)
            {
                if (highZones[i] != null)
                {
                    EditorUtility.SetDirty(highZones[i]);
                }
            }

            IReadOnlyList<LeiHuo.Gameplay.TemperatureField.ColdTemperatureZone> coldZones = modeSwitch.ColdTemperatureZones;
            for (int i = 0; i < coldZones.Count; i++)
            {
                if (coldZones[i] != null)
                {
                    EditorUtility.SetDirty(coldZones[i]);
                }
            }
        }
    }
}
