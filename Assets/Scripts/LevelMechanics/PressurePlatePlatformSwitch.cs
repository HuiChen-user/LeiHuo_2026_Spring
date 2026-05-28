using System.Collections.Generic;
using UnityEngine;

namespace LeiHuo.Gameplay.LevelMechanics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class PressurePlatePlatformSwitch : MonoBehaviour
    {
        [Header("Pressure Plate")]
        [SerializeField] private LayerMask triggerLayers = ~0;
        [SerializeField] private bool ignoreSelfHierarchy = true;
        [SerializeField] private bool requireEnabledColliders = true;

        [Header("Platform")]
        [SerializeField] private Transform platform;
        [SerializeField] private Transform pointSpace;
        [SerializeField] private Vector3 inactivePoint;
        [SerializeField] private Vector3 activePoint = Vector3.up * 3f;
        [SerializeField] private bool snapPlatformToInactiveOnPlay = true;
        [SerializeField] private bool useRigidbodyWhenAvailable = true;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(0.001f)] private float arriveDistance = 0.02f;
        [SerializeField] private bool easeMovement = true;
        [SerializeField, Min(0.01f)] private float easeSharpness = 8f;

        [Header("Plate Visual")]
        [SerializeField] private Transform plateVisual;
        [SerializeField] private Vector3 releasedLocalOffset;
        [SerializeField] private Vector3 pressedLocalOffset = Vector3.down * 0.08f;
        [SerializeField, Min(0f)] private float plateVisualSpeed = 12f;

        [Header("Debug Preview")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color inactiveColor = new Color(0.95f, 0.8f, 0.25f, 0.95f);
        [SerializeField] private Color activeColor = new Color(0.25f, 0.9f, 0.45f, 0.95f);
        [SerializeField] private Color lineColor = new Color(0.3f, 0.8f, 1f, 0.8f);
        [SerializeField, Min(0.05f)] private float pointGizmoRadius = 0.25f;
        [SerializeField] private bool logStateChanges;

        private readonly HashSet<Collider> pressingColliders = new HashSet<Collider>();
        private Rigidbody platformBody;
        private Vector3 releasedVisualLocalPosition;
        private bool wasPressed;

        public bool IsPressed => pressingColliders.Count > 0;
        public Transform Platform => platform;
        public Transform PointSpace => pointSpace;
        public bool ShowGizmos => showGizmos;
        public Color InactiveColor => inactiveColor;
        public Color ActiveColor => activeColor;
        public Color LineColor => lineColor;
        public float PointGizmoRadius => pointGizmoRadius;

        private void Reset()
        {
            Collider plateCollider = GetComponent<Collider>();
            plateCollider.isTrigger = true;

            pointSpace = transform.parent;
            releasedLocalOffset = Vector3.zero;
            pressedLocalOffset = Vector3.down * 0.08f;
        }

        private void Awake()
        {
            EnsureTriggerCollider();
            CachePlatformBody();

            if (plateVisual != null)
            {
                releasedVisualLocalPosition = plateVisual.localPosition;
            }

            if (platform != null && snapPlatformToInactiveOnPlay)
            {
                ApplyPlatformPosition(GetInactiveWorldPoint(), true);
            }
        }

        private void OnEnable()
        {
            CleanupPressingColliders();
            wasPressed = IsPressed;
        }

        private void Update()
        {
            if (platformBody != null && useRigidbodyWhenAvailable)
            {
                UpdatePlateVisual(Time.deltaTime);
                return;
            }

            TickPlatform(Time.deltaTime);
            UpdatePlateVisual(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (platformBody == null || !useRigidbodyWhenAvailable)
            {
                return;
            }

            TickPlatform(Time.fixedDeltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanColliderPress(other))
            {
                return;
            }

            pressingColliders.Add(other);
            ReportPressStateChange();
        }

        private void OnTriggerStay(Collider other)
        {
            if (!CanColliderPress(other))
            {
                pressingColliders.Remove(other);
                ReportPressStateChange();
                return;
            }

            pressingColliders.Add(other);
            ReportPressStateChange();
        }

        private void OnTriggerExit(Collider other)
        {
            if (pressingColliders.Remove(other))
            {
                ReportPressStateChange();
            }
        }

        public Vector3 GetInactiveWorldPoint()
        {
            return PointToWorld(inactivePoint);
        }

        public Vector3 GetActiveWorldPoint()
        {
            return PointToWorld(activePoint);
        }

        public void SetInactiveWorldPoint(Vector3 worldPoint)
        {
            inactivePoint = WorldToPoint(worldPoint);
        }

        public void SetActiveWorldPoint(Vector3 worldPoint)
        {
            activePoint = WorldToPoint(worldPoint);
        }

        public void CaptureInactiveFromPlatform()
        {
            if (platform != null)
            {
                SetInactiveWorldPoint(platform.position);
            }
        }

        public void CaptureActiveFromPlatform()
        {
            if (platform != null)
            {
                SetActiveWorldPoint(platform.position);
            }
        }

        private void TickPlatform(float deltaTime)
        {
            if (platform == null || moveSpeed <= 0f || deltaTime <= 0f)
            {
                return;
            }

            CleanupPressingColliders();

            Vector3 currentPosition = platformBody != null && useRigidbodyWhenAvailable
                ? platformBody.position
                : platform.position;
            Vector3 targetPosition = IsPressed ? GetActiveWorldPoint() : GetInactiveWorldPoint();
            Vector3 nextPosition = CalculateNextPosition(currentPosition, targetPosition, deltaTime);

            if (Vector3.Distance(nextPosition, targetPosition) <= arriveDistance)
            {
                nextPosition = targetPosition;
            }

            ApplyPlatformPosition(nextPosition, false);
        }

        private Vector3 CalculateNextPosition(Vector3 currentPosition, Vector3 targetPosition, float deltaTime)
        {
            if (!easeMovement)
            {
                return Vector3.MoveTowards(currentPosition, targetPosition, moveSpeed * deltaTime);
            }

            float t = 1f - Mathf.Exp(-easeSharpness * deltaTime);
            Vector3 easedPosition = Vector3.Lerp(currentPosition, targetPosition, t);
            return Vector3.MoveTowards(currentPosition, easedPosition, moveSpeed * deltaTime);
        }

        private void ApplyPlatformPosition(Vector3 nextPosition, bool forceTransform)
        {
            if (!forceTransform && platformBody != null && useRigidbodyWhenAvailable)
            {
                platformBody.MovePosition(nextPosition);
                return;
            }

            platform.position = nextPosition;
            if (platformBody != null && forceTransform)
            {
                platformBody.position = nextPosition;
            }
        }

        private void UpdatePlateVisual(float deltaTime)
        {
            if (plateVisual == null || deltaTime <= 0f)
            {
                return;
            }

            Vector3 targetPosition = releasedVisualLocalPosition + (IsPressed ? pressedLocalOffset : releasedLocalOffset);
            if (plateVisualSpeed <= 0f)
            {
                plateVisual.localPosition = targetPosition;
                return;
            }

            float t = 1f - Mathf.Exp(-plateVisualSpeed * deltaTime);
            plateVisual.localPosition = Vector3.Lerp(plateVisual.localPosition, targetPosition, t);
        }

        private bool CanColliderPress(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            if (requireEnabledColliders && !other.enabled)
            {
                return false;
            }

            if (ignoreSelfHierarchy && other.transform.IsChildOf(transform))
            {
                return false;
            }

            return (triggerLayers.value & (1 << other.gameObject.layer)) != 0;
        }

        private void CleanupPressingColliders()
        {
            pressingColliders.RemoveWhere(collider => collider == null || !CanColliderPress(collider));
        }

        private void ReportPressStateChange()
        {
            bool isPressed = IsPressed;
            if (wasPressed == isPressed)
            {
                return;
            }

            wasPressed = isPressed;
            if (logStateChanges)
            {
                Debug.Log($"{name} pressure plate {(isPressed ? "pressed" : "released")}.", this);
            }
        }

        private void CachePlatformBody()
        {
            platformBody = platform != null ? platform.GetComponent<Rigidbody>() : null;
        }

        private Vector3 PointToWorld(Vector3 point)
        {
            return pointSpace != null ? pointSpace.TransformPoint(point) : point;
        }

        private Vector3 WorldToPoint(Vector3 worldPoint)
        {
            return pointSpace != null ? pointSpace.InverseTransformPoint(worldPoint) : worldPoint;
        }

        private void EnsureTriggerCollider()
        {
            Collider plateCollider = GetComponent<Collider>();
            if (plateCollider != null)
            {
                plateCollider.isTrigger = true;
            }
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            arriveDistance = Mathf.Max(0.001f, arriveDistance);
            easeSharpness = Mathf.Max(0.01f, easeSharpness);
            plateVisualSpeed = Mathf.Max(0f, plateVisualSpeed);
            pointGizmoRadius = Mathf.Max(0.05f, pointGizmoRadius);
            CachePlatformBody();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos)
            {
                return;
            }

            Vector3 inactiveWorldPoint = GetInactiveWorldPoint();
            Vector3 activeWorldPoint = GetActiveWorldPoint();

            Gizmos.color = lineColor;
            Gizmos.DrawLine(inactiveWorldPoint, activeWorldPoint);

            Gizmos.color = inactiveColor;
            Gizmos.DrawSphere(inactiveWorldPoint, pointGizmoRadius);

            Gizmos.color = activeColor;
            Gizmos.DrawSphere(activeWorldPoint, pointGizmoRadius);
        }
    }
}
