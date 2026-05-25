using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LeiHuo.Gameplay.LevelMechanics
{
    public class IceBlockCarryController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode aimMouseButton = KeyCode.Mouse1;
        [SerializeField] private KeyCode carryMouseButton = KeyCode.Mouse0;
        [SerializeField] private bool requireAimButtonWhileCarrying = true;

        [Header("Aim")]
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform rangeOrigin;
        [SerializeField] private LayerMask carryableLayers = ~0;
        [SerializeField, Min(0.1f)] private float maxAimDistance = 6f;
        [SerializeField, Min(0.1f)] private float maxCarryDistanceFromOrigin = 6f;
        [SerializeField, Min(0.1f)] private float minHoldDistance = 1.2f;
        [SerializeField, Min(0.1f)] private float maxHoldDistance = 5f;
        [SerializeField] private bool preserveGrabbedSurfacePoint = true;
        [SerializeField] private bool clampHoldDistance = true;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
        [SerializeField, Min(0f)] private float aimAssistRadius = 0.2f;
        [SerializeField, Min(1)] private int maxAimHits = 16;
        [SerializeField] private bool ignoreControllerHierarchy = true;

        [Header("Carry Motion")]
        [SerializeField] private bool faceCameraYaw = true;
        [SerializeField] private bool releaseWhenBlockedByRange = true;

        [Header("Crosshair")]
        [SerializeField] private GameObject crosshairObject;
        [SerializeField] private bool drawFallbackCrosshair = true;
        [SerializeField, Min(4f)] private float crosshairSize = 18f;
        [SerializeField, Min(1f)] private float crosshairGap = 5f;
        [SerializeField, Min(1f)] private float crosshairThickness = 2f;
        [SerializeField] private Color crosshairColor = Color.white;
        [SerializeField] private Color targetCrosshairColor = new Color(0.35f, 0.9f, 1f, 1f);

        [Header("Debug Preview")]
        [SerializeField] private bool showRangeGizmo = true;
        [SerializeField] private Color aimRangeColor = new Color(0.35f, 0.9f, 1f, 0.25f);
        [SerializeField] private Color carryRangeColor = new Color(0.2f, 0.65f, 1f, 0.18f);
        [SerializeField] private bool logStateChanges;

        private CarryableIceBlock carriedBlock;
        private float holdDistance;
        private Vector3 cameraLocalGrabOffset;
        private Vector3 lastCarryPosition;
        private Vector3 releaseVelocity;
        private bool isAimingAtCarryable;
        private Texture2D crosshairTexture;
        private RaycastHit[] aimHits;

        private bool IsAimHeld => IsKeyHeld(aimMouseButton);
        private bool IsCarryHeld => IsKeyHeld(carryMouseButton);
        private bool ShouldShowCrosshair => IsAimHeld || carriedBlock != null;

        public bool IsCarrying => carriedBlock != null;

        private void Awake()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (rangeOrigin == null)
            {
                rangeOrigin = transform;
            }

            AllocateAimHits();
            SetCrosshairVisible(false);
        }

        private void Update()
        {
            bool shouldCarry = IsCarryHeld && (!requireAimButtonWhileCarrying || IsAimHeld);
            if (!shouldCarry)
            {
                ReleaseCurrentBlock();
                SetCrosshairVisible(ShouldShowCrosshair);
                isAimingAtCarryable = IsAimHeld && IsAimingAtAvailableBlock();
                return;
            }

            SetCrosshairVisible(ShouldShowCrosshair);

            if (carriedBlock == null)
            {
                if (IsAimHeld)
                {
                    TryAcquireBlock();
                }
            }
            else
            {
                UpdateCarriedBlock();
            }
        }

        private bool IsAimingAtAvailableBlock()
        {
            Ray ray = CreateAimRay();
            return TryFindCarryableHit(ray, out CarryableIceBlock target, out RaycastHit targetHit);
        }

        private bool TryFindCarryableHit(Ray ray, out CarryableIceBlock target, out RaycastHit targetHit)
        {
            AllocateAimHits();
            target = null;
            targetHit = new RaycastHit();

            int hitCount = Physics.RaycastNonAlloc(ray, aimHits, maxAimDistance, carryableLayers, triggerInteraction);
            if (TryPickCarryableFromHits(hitCount, out target, out targetHit))
            {
                return true;
            }

            if (aimAssistRadius <= 0f)
            {
                return false;
            }

            hitCount = Physics.SphereCastNonAlloc(ray, aimAssistRadius, aimHits, maxAimDistance, carryableLayers, triggerInteraction);
            return TryPickCarryableFromHits(hitCount, out target, out targetHit);
        }

        private bool TryPickCarryableFromHits(int hitCount, out CarryableIceBlock target, out RaycastHit targetHit)
        {
            target = null;
            targetHit = new RaycastHit();

            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = aimHits[i];
                if (hit.collider == null || ShouldIgnoreHit(hit.collider))
                {
                    continue;
                }

                CarryableIceBlock candidate = hit.collider.GetComponentInParent<CarryableIceBlock>();
                if (candidate == null || !candidate.CanBeCarried || !IsWithinCarryOriginRange(candidate.transform.position))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    target = candidate;
                    targetHit = hit;
                }
            }

            return target != null;
        }

        private bool ShouldIgnoreHit(Collider hitCollider)
        {
            return ignoreControllerHierarchy && hitCollider.transform.IsChildOf(transform);
        }

        private void TryAcquireBlock()
        {
            Ray ray = CreateAimRay();
            isAimingAtCarryable = false;

            if (!TryFindCarryableHit(ray, out CarryableIceBlock target, out RaycastHit hit))
            {
                return;
            }

            isAimingAtCarryable = true;
            if (!target.TryBeginCarry())
            {
                return;
            }

            carriedBlock = target;
            holdDistance = clampHoldDistance ? Mathf.Clamp(hit.distance, minHoldDistance, maxHoldDistance) : hit.distance;
            cameraLocalGrabOffset = preserveGrabbedSurfacePoint && aimCamera != null
                ? Quaternion.Inverse(aimCamera.transform.rotation) * (target.transform.position - hit.point)
                : Vector3.zero;
            lastCarryPosition = target.transform.position;
            releaseVelocity = Vector3.zero;

            if (logStateChanges)
            {
                Debug.Log($"{name} picked up {target.name}.", target);
            }
        }

        private void UpdateCarriedBlock()
        {
            Ray ray = CreateAimRay();
            Vector3 targetPosition = ray.GetPoint(holdDistance);
            if (preserveGrabbedSurfacePoint && aimCamera != null)
            {
                targetPosition += aimCamera.transform.rotation * cameraLocalGrabOffset;
            }

            if (!IsWithinCarryOriginRange(targetPosition))
            {
                if (releaseWhenBlockedByRange)
                {
                    ReleaseCurrentBlock();
                }

                return;
            }

            Quaternion targetRotation = faceCameraYaw && aimCamera != null
                ? Quaternion.Euler(0f, aimCamera.transform.eulerAngles.y, 0f)
                : carriedBlock.transform.rotation;

            carriedBlock.CarryTo(targetPosition, targetRotation);
            releaseVelocity = (carriedBlock.transform.position - lastCarryPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastCarryPosition = carriedBlock.transform.position;
        }

        private void ReleaseCurrentBlock()
        {
            if (carriedBlock == null)
            {
                return;
            }

            carriedBlock.EndCarry(releaseVelocity);

            if (logStateChanges)
            {
                Debug.Log($"{name} released {carriedBlock.name}.", carriedBlock);
            }

            carriedBlock = null;
            releaseVelocity = Vector3.zero;
            cameraLocalGrabOffset = Vector3.zero;
        }

        private void AllocateAimHits()
        {
            if (aimHits == null || aimHits.Length != maxAimHits)
            {
                aimHits = new RaycastHit[maxAimHits];
            }
        }

        private Ray CreateAimRay()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            return aimCamera != null
                ? aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(transform.position + Vector3.up, transform.forward);
        }

        private bool IsWithinCarryOriginRange(Vector3 point)
        {
            Vector3 origin = rangeOrigin != null ? rangeOrigin.position : transform.position;
            return Vector3.Distance(origin, point) <= maxCarryDistanceFromOrigin;
        }

        private bool IsKeyHeld(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                if (keyCode == KeyCode.Mouse0)
                {
                    return Mouse.current.leftButton.isPressed;
                }

                if (keyCode == KeyCode.Mouse1)
                {
                    return Mouse.current.rightButton.isPressed;
                }

                if (keyCode == KeyCode.Mouse2)
                {
                    return Mouse.current.middleButton.isPressed;
                }
            }
#endif

            return Input.GetKey(keyCode);
        }

        private void SetCrosshairVisible(bool visible)
        {
            if (crosshairObject != null && crosshairObject.activeSelf != visible)
            {
                crosshairObject.SetActive(visible);
            }
        }

        private void OnGUI()
        {
            if (!drawFallbackCrosshair || crosshairObject != null || !ShouldShowCrosshair)
            {
                return;
            }

            EnsureCrosshairTexture();

            Color previousColor = GUI.color;
            GUI.color = isAimingAtCarryable || carriedBlock != null ? targetCrosshairColor : crosshairColor;

            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            float halfSize = crosshairSize * 0.5f;
            float halfThickness = crosshairThickness * 0.5f;

            GUI.DrawTexture(new Rect(centerX - crosshairGap - halfSize, centerY - halfThickness, halfSize, crosshairThickness), crosshairTexture);
            GUI.DrawTexture(new Rect(centerX + crosshairGap, centerY - halfThickness, halfSize, crosshairThickness), crosshairTexture);
            GUI.DrawTexture(new Rect(centerX - halfThickness, centerY - crosshairGap - halfSize, crosshairThickness, halfSize), crosshairTexture);
            GUI.DrawTexture(new Rect(centerX - halfThickness, centerY + crosshairGap, crosshairThickness, halfSize), crosshairTexture);

            GUI.color = previousColor;
        }

        private void EnsureCrosshairTexture()
        {
            if (crosshairTexture != null)
            {
                return;
            }

            crosshairTexture = Texture2D.whiteTexture;
        }

        private void OnValidate()
        {
            maxAimDistance = Mathf.Max(0.1f, maxAimDistance);
            maxCarryDistanceFromOrigin = Mathf.Max(0.1f, maxCarryDistanceFromOrigin);
            minHoldDistance = Mathf.Max(0.1f, minHoldDistance);
            maxHoldDistance = Mathf.Max(minHoldDistance, maxHoldDistance);
            aimAssistRadius = Mathf.Max(0f, aimAssistRadius);
            maxAimHits = Mathf.Max(1, maxAimHits);
            crosshairSize = Mathf.Max(4f, crosshairSize);
            crosshairGap = Mathf.Max(1f, crosshairGap);
            crosshairThickness = Mathf.Max(1f, crosshairThickness);
            AllocateAimHits();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showRangeGizmo)
            {
                return;
            }

            Transform origin = rangeOrigin != null ? rangeOrigin : transform;
            Gizmos.color = carryRangeColor;
            Gizmos.DrawWireSphere(origin.position, maxCarryDistanceFromOrigin);

            Camera gizmoCamera = aimCamera != null ? aimCamera : Camera.main;
            if (gizmoCamera == null)
            {
                return;
            }

            Ray ray = gizmoCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Gizmos.color = aimRangeColor;
            Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * maxAimDistance);
            Gizmos.DrawWireSphere(ray.origin + ray.direction * maxAimDistance, 0.12f);
            if (aimAssistRadius > 0f)
            {
                Gizmos.DrawWireSphere(ray.origin + ray.direction * Mathf.Min(maxAimDistance, maxHoldDistance), aimAssistRadius);
            }
        }
    }
}
