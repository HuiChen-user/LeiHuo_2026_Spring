using System.Collections.Generic;
using UnityEngine;

namespace LeiHuo.Gameplay.TemperatureField
{
    [DisallowMultipleComponent]
    public class HighTemperatureZone : MonoBehaviour
    {
        public enum ZoneShape
        {
            Sphere,
            Box
        }

        private static readonly List<HighTemperatureZone> ActiveZones = new List<HighTemperatureZone>();

        [Header("Shape")]
        [SerializeField] private ZoneShape shape = ZoneShape.Sphere;
        [SerializeField] private Vector3 centerOffset;
        [SerializeField, Min(0.01f)] private float radius = 5f;
        [SerializeField] private Vector3 boxSize = new Vector3(8f, 4f, 8f);

        [Header("Water Vapor")]
        [SerializeField] private bool requireEnhancedFieldToFreezeVapor = true;

        [Header("Uncontrolled Machine")]
        [SerializeField, Range(0f, 1f)] private float uncontrolledSlowSpeedMultiplier = 0.35f;
        [SerializeField, Min(0f)] private float uncontrolledSlowDurationAfterLeavingField = 2f;
        [SerializeField, Min(0f)] private float enhancedStopDurationAfterLeavingField = 1.2f;

        [Header("Debug Preview")]
        [SerializeField] private bool showZoneGizmo = true;
        [SerializeField] private Color zoneColor = new Color(1f, 0.16f, 0.05f, 0.22f);
        [SerializeField] private Color wireColor = new Color(1f, 0.3f, 0.12f, 0.85f);

        public bool RequireEnhancedFieldToFreezeVapor => requireEnhancedFieldToFreezeVapor;
        public float UncontrolledSlowSpeedMultiplier => uncontrolledSlowSpeedMultiplier;
        public float UncontrolledSlowDurationAfterLeavingField => uncontrolledSlowDurationAfterLeavingField;
        public float EnhancedStopDurationAfterLeavingField => enhancedStopDurationAfterLeavingField;

        public static bool TryGetZoneAtPosition(Vector3 worldPosition, out HighTemperatureZone zone)
        {
            for (int i = ActiveZones.Count - 1; i >= 0; i--)
            {
                HighTemperatureZone candidate = ActiveZones[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    ActiveZones.RemoveAt(i);
                    continue;
                }

                if (candidate.Contains(worldPosition))
                {
                    zone = candidate;
                    return true;
                }
            }

            zone = null;
            return false;
        }

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 localPoint = transform.InverseTransformPoint(worldPosition) - centerOffset;

            if (shape == ZoneShape.Sphere)
            {
                return localPoint.sqrMagnitude <= radius * radius;
            }

            Vector3 halfSize = GetSafeBoxSize() * 0.5f;
            return Mathf.Abs(localPoint.x) <= halfSize.x &&
                   Mathf.Abs(localPoint.y) <= halfSize.y &&
                   Mathf.Abs(localPoint.z) <= halfSize.z;
        }

        private void OnEnable()
        {
            if (!ActiveZones.Contains(this))
            {
                ActiveZones.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            boxSize = GetSafeBoxSize();
            uncontrolledSlowSpeedMultiplier = Mathf.Clamp01(uncontrolledSlowSpeedMultiplier);
            uncontrolledSlowDurationAfterLeavingField = Mathf.Max(0f, uncontrolledSlowDurationAfterLeavingField);
            enhancedStopDurationAfterLeavingField = Mathf.Max(0f, enhancedStopDurationAfterLeavingField);
        }

        private Vector3 GetSafeBoxSize()
        {
            return new Vector3(
                Mathf.Max(0.01f, boxSize.x),
                Mathf.Max(0.01f, boxSize.y),
                Mathf.Max(0.01f, boxSize.z));
        }

        private void OnDrawGizmos()
        {
            if (!showZoneGizmo)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.Translate(centerOffset);

            Gizmos.color = zoneColor;
            if (shape == ZoneShape.Sphere)
            {
                Gizmos.DrawSphere(Vector3.zero, radius);
            }
            else
            {
                Gizmos.DrawCube(Vector3.zero, GetSafeBoxSize());
            }

            Gizmos.color = wireColor;
            if (shape == ZoneShape.Sphere)
            {
                Gizmos.DrawWireSphere(Vector3.zero, radius);
            }
            else
            {
                Gizmos.DrawWireCube(Vector3.zero, GetSafeBoxSize());
            }

            Gizmos.matrix = previousMatrix;
        }
    }
}
