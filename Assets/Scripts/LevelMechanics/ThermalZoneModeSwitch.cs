using System;
using System.Collections.Generic;
using LeiHuo.Gameplay.TemperatureField;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LeiHuo.Gameplay.LevelMechanics
{
    [DisallowMultipleComponent]
    public class ThermalZoneModeSwitch : MonoBehaviour
    {
        private enum InteractionShape
        {
            Sphere,
            Box
        }

        private enum InitialMode
        {
            KeepSceneState,
            HighTemperature,
            ColdTemperature
        }

        [Header("Input")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        [Header("Interaction Range")]
        [SerializeField] private InteractionShape interactionShape = InteractionShape.Sphere;
        [SerializeField] private Vector3 interactionCenterOffset;
        [SerializeField, Min(0.01f)] private float interactionRadius = 2f;
        [SerializeField] private Vector3 interactionBoxSize = new Vector3(3f, 2f, 3f);
        [SerializeField] private LayerMask playerLayers = ~0;
        [SerializeField] private bool requireTemperatureFieldController = true;
        [SerializeField, Min(1)] private int maxDetectedColliders = 16;

        [Header("Thermal Zones")]
        [SerializeField] private List<HighTemperatureZone> highTemperatureZones = new List<HighTemperatureZone>();
        [SerializeField] private List<ColdTemperatureZone> coldTemperatureZones = new List<ColdTemperatureZone>();
        [SerializeField] private InitialMode initialMode = InitialMode.KeepSceneState;
        [SerializeField] private bool removeMissingZones = true;
        [SerializeField] private bool logStateChanges;

        [Header("Debug Preview")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color highModeRangeColor = new Color(1f, 0.18f, 0.05f, 0.28f);
        [SerializeField] private Color coldModeRangeColor = new Color(0.08f, 0.45f, 1f, 0.28f);
        [SerializeField] private Color wireColor = new Color(0.9f, 0.95f, 1f, 0.9f);
        [SerializeField] private Color highTargetLineColor = new Color(1f, 0.45f, 0.15f, 0.85f);
        [SerializeField] private Color coldTargetLineColor = new Color(0.35f, 0.8f, 1f, 0.85f);

        private Collider[] overlapBuffer;
        private bool isPlayerInRange;

        public bool ShowGizmos => showGizmos;
        public Color WireColor => wireColor;
        public Color HighTargetLineColor => highTargetLineColor;
        public Color ColdTargetLineColor => coldTargetLineColor;
        public bool IsPlayerInRange => isPlayerInRange;
        public bool IsHighModeActive => GetActiveHighZoneCount() > 0 && GetActiveColdZoneCount() == 0;
        public bool IsColdModeActive => GetActiveColdZoneCount() > 0 && GetActiveHighZoneCount() == 0;
        public IReadOnlyList<HighTemperatureZone> HighTemperatureZones => highTemperatureZones;
        public IReadOnlyList<ColdTemperatureZone> ColdTemperatureZones => coldTemperatureZones;

        private Vector3 InteractionWorldCenter => transform.TransformPoint(interactionCenterOffset);

        private void Awake()
        {
            AllocateOverlapBuffer();
        }

        private void Start()
        {
            if (initialMode == InitialMode.HighTemperature)
            {
                SetHighTemperatureMode();
            }
            else if (initialMode == InitialMode.ColdTemperature)
            {
                SetColdTemperatureMode();
            }
        }

        private void Update()
        {
            DetectPlayerInRange();

            if (isPlayerInRange && WasInteractionPressedThisFrame())
            {
                ToggleMode();
            }
        }

        public void ToggleMode()
        {
            if (IsHighModeActive)
            {
                SetColdTemperatureMode();
            }
            else
            {
                SetHighTemperatureMode();
            }
        }

        public void SetHighTemperatureMode()
        {
            SetColdZonesActive(false);
            SetHighZonesActive(true);

            if (logStateChanges)
            {
                Debug.Log($"{name} switched thermal zones to high temperature.", this);
            }
        }

        public void SetColdTemperatureMode()
        {
            SetHighZonesActive(false);
            SetColdZonesActive(true);

            if (logStateChanges)
            {
                Debug.Log($"{name} switched thermal zones to cold temperature.", this);
            }
        }

        public int GetValidHighZoneCount()
        {
            int count = 0;
            for (int i = 0; i < highTemperatureZones.Count; i++)
            {
                if (highTemperatureZones[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        public int GetValidColdZoneCount()
        {
            int count = 0;
            for (int i = 0; i < coldTemperatureZones.Count; i++)
            {
                if (coldTemperatureZones[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        public int GetActiveHighZoneCount()
        {
            int count = 0;
            for (int i = 0; i < highTemperatureZones.Count; i++)
            {
                if (highTemperatureZones[i] != null && highTemperatureZones[i].isActiveAndEnabled)
                {
                    count++;
                }
            }

            return count;
        }

        public int GetActiveColdZoneCount()
        {
            int count = 0;
            for (int i = 0; i < coldTemperatureZones.Count; i++)
            {
                if (coldTemperatureZones[i] != null && coldTemperatureZones[i].isActiveAndEnabled)
                {
                    count++;
                }
            }

            return count;
        }

        public Vector3 GetInteractionWorldCenter()
        {
            return InteractionWorldCenter;
        }

        public float GetInteractionRadius()
        {
            return interactionRadius;
        }

        public void SetInteractionRadius(float radius)
        {
            interactionRadius = Mathf.Max(0.01f, radius);
        }

        public Vector3 GetInteractionBoxSize()
        {
            return GetSafeInteractionBoxSize();
        }

        public void SetInteractionBoxSize(Vector3 size)
        {
            interactionBoxSize = new Vector3(
                Mathf.Max(0.01f, size.x),
                Mathf.Max(0.01f, size.y),
                Mathf.Max(0.01f, size.z));
        }

        public bool UsesSphereRange()
        {
            return interactionShape == InteractionShape.Sphere;
        }

        private void SetHighZonesActive(bool active)
        {
            for (int i = highTemperatureZones.Count - 1; i >= 0; i--)
            {
                HighTemperatureZone zone = highTemperatureZones[i];
                if (zone == null)
                {
                    if (removeMissingZones)
                    {
                        highTemperatureZones.RemoveAt(i);
                    }

                    continue;
                }

                zone.enabled = active;
                if (active)
                {
                    ColdTemperatureZone.DisableOverlappingZones(zone);
                }
            }
        }

        private void SetColdZonesActive(bool active)
        {
            for (int i = coldTemperatureZones.Count - 1; i >= 0; i--)
            {
                ColdTemperatureZone zone = coldTemperatureZones[i];
                if (zone == null)
                {
                    if (removeMissingZones)
                    {
                        coldTemperatureZones.RemoveAt(i);
                    }

                    continue;
                }

                zone.enabled = active;
                if (active)
                {
                    HighTemperatureZone.DisableOverlappingZones(zone);
                }
            }
        }

        private void DetectPlayerInRange()
        {
            AllocateOverlapBuffer();
            isPlayerInRange = false;

            int hitCount = interactionShape == InteractionShape.Sphere
                ? Physics.OverlapSphereNonAlloc(
                    InteractionWorldCenter,
                    interactionRadius,
                    overlapBuffer,
                    playerLayers,
                    QueryTriggerInteraction.Collide)
                : Physics.OverlapBoxNonAlloc(
                    InteractionWorldCenter,
                    GetSafeInteractionBoxSize() * 0.5f,
                    overlapBuffer,
                    transform.rotation,
                    playerLayers,
                    QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = overlapBuffer[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                TemperatureFieldController controller = hit.GetComponentInParent<TemperatureFieldController>();
                if (requireTemperatureFieldController && controller == null)
                {
                    continue;
                }

                isPlayerInRange = true;
                return;
            }
        }

        private bool WasInteractionPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && TryGetInputSystemKey(interactionKey, out Key key))
            {
                return Keyboard.current[key].wasPressedThisFrame;
            }
#endif

            return Input.GetKeyDown(interactionKey);
        }

#if ENABLE_INPUT_SYSTEM
        private bool TryGetInputSystemKey(KeyCode keyCode, out Key key)
        {
            try
            {
                key = (Key)Enum.Parse(typeof(Key), keyCode.ToString());
                return true;
            }
            catch (ArgumentException)
            {
                key = Key.None;
                return false;
            }
        }
#endif

        private void AllocateOverlapBuffer()
        {
            if (overlapBuffer == null || overlapBuffer.Length != maxDetectedColliders)
            {
                overlapBuffer = new Collider[maxDetectedColliders];
            }
        }

        private Vector3 GetSafeInteractionBoxSize()
        {
            return new Vector3(
                Mathf.Max(0.01f, interactionBoxSize.x),
                Mathf.Max(0.01f, interactionBoxSize.y),
                Mathf.Max(0.01f, interactionBoxSize.z));
        }

        private void OnValidate()
        {
            interactionRadius = Mathf.Max(0.01f, interactionRadius);
            interactionBoxSize = GetSafeInteractionBoxSize();
            maxDetectedColliders = Mathf.Max(1, maxDetectedColliders);

            if (overlapBuffer == null || overlapBuffer.Length != maxDetectedColliders)
            {
                AllocateOverlapBuffer();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos)
            {
                return;
            }

            DrawInteractionRangeGizmo();
            DrawTargetLinesGizmo();
        }

        private void DrawInteractionRangeGizmo()
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(InteractionWorldCenter, transform.rotation, Vector3.one);

            Gizmos.color = IsColdModeActive ? coldModeRangeColor : highModeRangeColor;
            if (interactionShape == InteractionShape.Sphere)
            {
                Gizmos.DrawSphere(Vector3.zero, interactionRadius);
            }
            else
            {
                Gizmos.DrawCube(Vector3.zero, GetSafeInteractionBoxSize());
            }

            Gizmos.color = wireColor;
            if (interactionShape == InteractionShape.Sphere)
            {
                Gizmos.DrawWireSphere(Vector3.zero, interactionRadius);
            }
            else
            {
                Gizmos.DrawWireCube(Vector3.zero, GetSafeInteractionBoxSize());
            }

            Gizmos.matrix = previousMatrix;
        }

        private void DrawTargetLinesGizmo()
        {
            Vector3 from = InteractionWorldCenter;

            Gizmos.color = highTargetLineColor;
            for (int i = 0; i < highTemperatureZones.Count; i++)
            {
                HighTemperatureZone zone = highTemperatureZones[i];
                if (zone != null)
                {
                    Gizmos.DrawLine(from, zone.transform.position);
                }
            }

            Gizmos.color = coldTargetLineColor;
            for (int i = 0; i < coldTemperatureZones.Count; i++)
            {
                ColdTemperatureZone zone = coldTemperatureZones[i];
                if (zone != null)
                {
                    Gizmos.DrawLine(from, zone.transform.position);
                }
            }
        }
    }
}
