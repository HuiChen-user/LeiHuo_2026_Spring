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
    public class ColdTemperatureZoneSwitch : MonoBehaviour
    {
        private enum InteractionShape
        {
            Sphere,
            Box
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

        [Header("Controlled Zones")]
        [SerializeField] private List<ColdTemperatureZone> controlledZones = new List<ColdTemperatureZone>();
        [SerializeField] private bool disableControlledZonesOnStart = true;
        [SerializeField] private bool removeMissingZones = true;
        [SerializeField] private bool logStateChanges;

        [Header("Debug Preview")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color inactiveRangeColor = new Color(0.1f, 0.45f, 1f, 0.18f);
        [SerializeField] private Color activeRangeColor = new Color(0.08f, 0.75f, 1f, 0.32f);
        [SerializeField] private Color wireColor = new Color(0.3f, 0.75f, 1f, 0.9f);
        [SerializeField] private Color targetLineColor = new Color(0.45f, 0.9f, 1f, 0.85f);

        private Collider[] overlapBuffer;
        private bool isPlayerInRange;

        public bool ShowGizmos => showGizmos;
        public Color WireColor => wireColor;
        public Color TargetLineColor => targetLineColor;
        public bool IsPlayerInRange => isPlayerInRange;
        public bool AreAllZonesActive => GetValidZoneCount() > 0 && GetActiveZoneCount() == GetValidZoneCount();
        public IReadOnlyList<ColdTemperatureZone> ControlledZones => controlledZones;

        private Vector3 InteractionWorldCenter => transform.TransformPoint(interactionCenterOffset);

        private void Awake()
        {
            AllocateOverlapBuffer();
        }

        private void Start()
        {
            if (disableControlledZonesOnStart)
            {
                SetControlledZonesActive(false);
            }
        }

        private void Update()
        {
            DetectPlayerInRange();

            if (isPlayerInRange && WasInteractionPressedThisFrame())
            {
                ToggleControlledZones();
            }
        }

        public void ToggleControlledZones()
        {
            bool targetActive = !AreAllZonesActive;
            SetControlledZonesActive(targetActive);
        }

        public void SetControlledZonesActive(bool active)
        {
            for (int i = controlledZones.Count - 1; i >= 0; i--)
            {
                ColdTemperatureZone zone = controlledZones[i];
                if (zone == null)
                {
                    if (removeMissingZones)
                    {
                        controlledZones.RemoveAt(i);
                    }

                    continue;
                }

                zone.enabled = active;
            }

            if (logStateChanges)
            {
                Debug.Log($"{name} turned controlled cold-temperature zones {(active ? "on" : "off")}.", this);
            }
        }

        public int GetValidZoneCount()
        {
            int count = 0;
            for (int i = 0; i < controlledZones.Count; i++)
            {
                if (controlledZones[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        public int GetActiveZoneCount()
        {
            int count = 0;
            for (int i = 0; i < controlledZones.Count; i++)
            {
                if (controlledZones[i] != null && controlledZones[i].isActiveAndEnabled)
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

            Gizmos.color = AreAllZonesActive ? activeRangeColor : inactiveRangeColor;
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
            Gizmos.color = targetLineColor;
            Vector3 from = InteractionWorldCenter;

            for (int i = 0; i < controlledZones.Count; i++)
            {
                ColdTemperatureZone zone = controlledZones[i];
                if (zone != null)
                {
                    Gizmos.DrawLine(from, zone.transform.position);
                }
            }
        }
    }
}
