using System.Collections.Generic;
using UnityEngine;

namespace LeiHuo.Gameplay.TemperatureField
{
    [DisallowMultipleComponent]
    public class ColdTemperatureZone : MonoBehaviour
    {
        public enum ZoneShape
        {
            Sphere,
            Box
        }

        private static readonly List<ColdTemperatureZone> ActiveZones = new List<ColdTemperatureZone>();

        [Header("Shape")]
        [SerializeField] private ZoneShape shape = ZoneShape.Sphere;
        [SerializeField] private Vector3 centerOffset;
        [SerializeField, Min(0.01f)] private float radius = 5f;
        [SerializeField] private Vector3 boxSize = new Vector3(8f, 4f, 8f);

        [Header("Debug Preview")]
        [SerializeField] private bool showZoneGizmo = true;
        [SerializeField] private Color zoneColor = new Color(0.1f, 0.45f, 1f, 0.2f);
        [SerializeField] private Color wireColor = new Color(0.35f, 0.75f, 1f, 0.9f);

        public ZoneShape CurrentShape => shape;
        public Vector3 WorldCenter => transform.TransformPoint(centerOffset);
        public Quaternion WorldRotation => transform.rotation;
        public float Radius => radius;
        public Vector3 BoxSize => GetSafeBoxSize();

        public static void DisableOverlappingZones(HighTemperatureZone highZone)
        {
            for (int i = ActiveZones.Count - 1; i >= 0; i--)
            {
                ColdTemperatureZone candidate = ActiveZones[i];
                if (candidate == null)
                {
                    ActiveZones.RemoveAt(i);
                    continue;
                }

                if (candidate.isActiveAndEnabled && TemperatureZoneOverlapUtility.Overlaps(highZone, candidate))
                {
                    candidate.enabled = false;
                }
            }
        }

        public static bool TryGetZoneAtPosition(Vector3 worldPosition, out ColdTemperatureZone zone)
        {
            for (int i = ActiveZones.Count - 1; i >= 0; i--)
            {
                ColdTemperatureZone candidate = ActiveZones[i];
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
