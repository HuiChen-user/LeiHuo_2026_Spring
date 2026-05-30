using System.Collections.Generic;
using UnityEngine;

namespace LeiHuo.Gameplay.LevelMechanics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class IceLiftDoor : MonoBehaviour
    {
        [Header("Door")]
        [SerializeField] private Transform door;
        [SerializeField, Min(0f)] private float maxLiftHeight = 3f;
        [SerializeField, Min(0f)] private float extraLiftClearance = 0.02f;
        [SerializeField] private bool snapClosedOnPlay;
        [SerializeField] private bool useRigidbodyWhenAvailable = true;
        [SerializeField] private bool ensureKinematicRigidbody = true;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(0.001f)] private float arriveDistance = 0.01f;
        [SerializeField] private bool easeMovement = true;
        [SerializeField, Min(0.01f)] private float easeSharpness = 10f;

        [Header("Ice Detection")]
        [Tooltip("Ice blocks are accepted when they have IceBlockTemperatureState, or when their layer is included here.")]
        [SerializeField] private LayerMask iceLayers = 0;
        [SerializeField, Min(0.01f)] private float supportDepthBelowDoor = 1f;
        [SerializeField, Min(0f)] private float footprintPadding = 0.05f;
        [SerializeField, Min(1)] private int maxDetectedColliders = 24;
        [SerializeField] private QueryTriggerInteraction iceTriggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Collision Filtering")]
        [Tooltip("Player colliders are accepted when they are on these layers or have a CharacterController in their parent hierarchy.")]
        [SerializeField] private LayerMask playerLayers = 0;
        [SerializeField] private bool ignoreNonPlayerAndNonIceColliders = true;
        [SerializeField, Min(0f)] private float ignoreSearchPadding = 0.25f;
        [SerializeField, Min(1)] private int maxIgnoredColliders = 64;

        [Header("Debug Preview")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color closedColor = new Color(0.95f, 0.55f, 0.2f, 0.9f);
        [SerializeField] private Color openColor = new Color(0.25f, 0.9f, 0.95f, 0.9f);
        [SerializeField] private Color scanColor = new Color(0.35f, 0.85f, 1f, 0.2f);
        [SerializeField] private bool logStateChanges;

        private readonly HashSet<Collider> ignoredColliders = new HashSet<Collider>();
        private Collider doorCollider;
        private Rigidbody doorBody;
        private Collider[] iceBuffer;
        private Collider[] ignoreBuffer;
        private Vector3 closedPosition;
        private Bounds closedBounds;
        private bool wasLifted;

        private Transform DoorTransform => door != null ? door : transform;
        private bool CanUseRigidbody => doorBody != null && useRigidbodyWhenAvailable;
        private void Reset()
        {
            door = transform;
        }

        private void Awake()
        {
            door = DoorTransform;
            doorCollider = door.GetComponent<Collider>();
            if (doorCollider == null)
            {
                doorCollider = GetComponent<Collider>();
            }

            CacheClosedState();
            EnsureRigidbodySetup();
            EnsureBuffers();

            if (snapClosedOnPlay)
            {
                ApplyDoorPosition(closedPosition, true);
            }

            RefreshIgnoredCollisions();
        }

        private void OnEnable()
        {
            EnsureBuffers();
            RefreshIgnoredCollisions();
        }

        private void OnDisable()
        {
            RestoreIgnoredCollisions();
        }

        private void Update()
        {
            if (CanUseRigidbody)
            {
                return;
            }

            TickDoor(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (!CanUseRigidbody)
            {
                return;
            }

            TickDoor(Time.fixedDeltaTime);
        }

        private void TickDoor(float deltaTime)
        {
            if (door == null || doorCollider == null || deltaTime <= 0f)
            {
                return;
            }

            RefreshIgnoredCollisions();

            float liftHeight = CalculateLiftHeightFromIce();
            Vector3 targetPosition = closedPosition + Vector3.up * liftHeight;
            Vector3 currentPosition = CanUseRigidbody ? doorBody.position : door.position;
            Vector3 nextPosition = CalculateNextPosition(currentPosition, targetPosition, deltaTime);

            if (Vector3.Distance(nextPosition, targetPosition) <= arriveDistance)
            {
                nextPosition = targetPosition;
            }

            ApplyDoorPosition(nextPosition, false);
            ReportLiftState(liftHeight > arriveDistance);
        }

        private float CalculateLiftHeightFromIce()
        {
            Bounds scanBounds = GetIceScanBounds();
            int hitCount = Physics.OverlapBoxNonAlloc(
                scanBounds.center,
                scanBounds.extents,
                iceBuffer,
                Quaternion.identity,
                Physics.AllLayers,
                iceTriggerInteraction);

            float highestIceTop = float.NegativeInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = iceBuffer[i];
                if (!CanIceLiftDoor(candidate))
                {
                    continue;
                }

                highestIceTop = Mathf.Max(highestIceTop, candidate.bounds.max.y);
            }

            if (float.IsNegativeInfinity(highestIceTop))
            {
                return 0f;
            }

            float liftHeight = highestIceTop - closedBounds.min.y + extraLiftClearance;
            return Mathf.Clamp(liftHeight, 0f, maxLiftHeight);
        }

        private Vector3 CalculateNextPosition(Vector3 currentPosition, Vector3 targetPosition, float deltaTime)
        {
            if (moveSpeed <= 0f)
            {
                return targetPosition;
            }

            if (!easeMovement)
            {
                return Vector3.MoveTowards(currentPosition, targetPosition, moveSpeed * deltaTime);
            }

            float t = 1f - Mathf.Exp(-easeSharpness * deltaTime);
            Vector3 easedPosition = Vector3.Lerp(currentPosition, targetPosition, t);
            return Vector3.MoveTowards(currentPosition, easedPosition, moveSpeed * deltaTime);
        }

        private void ApplyDoorPosition(Vector3 nextPosition, bool forceTransform)
        {
            if (!forceTransform && CanUseRigidbody)
            {
                doorBody.MovePosition(nextPosition);
                return;
            }

            door.position = nextPosition;
            if (doorBody != null && forceTransform)
            {
                doorBody.position = nextPosition;
            }
        }

        private void RefreshIgnoredCollisions()
        {
            if (!ignoreNonPlayerAndNonIceColliders || doorCollider == null)
            {
                return;
            }

            Bounds searchBounds = GetIgnoreSearchBounds();
            int hitCount = Physics.OverlapBoxNonAlloc(
                searchBounds.center,
                searchBounds.extents,
                ignoreBuffer,
                Quaternion.identity,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = ignoreBuffer[i];
                if (!ShouldIgnoreCollision(candidate))
                {
                    continue;
                }

                if (ignoredColliders.Add(candidate))
                {
                    Physics.IgnoreCollision(doorCollider, candidate, true);
                }
            }
        }

        private void RestoreIgnoredCollisions()
        {
            if (doorCollider == null)
            {
                ignoredColliders.Clear();
                return;
            }

            foreach (Collider ignoredCollider in ignoredColliders)
            {
                if (ignoredCollider != null)
                {
                    Physics.IgnoreCollision(doorCollider, ignoredCollider, false);
                }
            }

            ignoredColliders.Clear();
        }

        private bool ShouldIgnoreCollision(Collider other)
        {
            if (other == null || other == doorCollider || other.transform.IsChildOf(door))
            {
                return false;
            }

            return !CanPlayerCollideWithDoor(other) && !CanIceLiftDoor(other);
        }

        private bool CanPlayerCollideWithDoor(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            if ((playerLayers.value & (1 << other.gameObject.layer)) != 0)
            {
                return true;
            }

            return other.GetComponentInParent<CharacterController>() != null;
        }

        private bool CanIceLiftDoor(Collider other)
        {
            if (other == null || !other.enabled || other == doorCollider || other.transform.IsChildOf(door))
            {
                return false;
            }

            if (other.GetComponentInParent<IceBlockTemperatureState>() != null)
            {
                return true;
            }

            return (iceLayers.value & (1 << other.gameObject.layer)) != 0;
        }

        private Bounds GetIceScanBounds()
        {
            Vector3 size = closedBounds.size;
            size.x += footprintPadding * 2f;
            size.z += footprintPadding * 2f;
            size.y = maxLiftHeight + supportDepthBelowDoor + extraLiftClearance;

            Vector3 center = closedBounds.center;
            center.y = closedBounds.min.y + (maxLiftHeight - supportDepthBelowDoor + extraLiftClearance) * 0.5f;
            return new Bounds(center, size);
        }

        private Bounds GetIgnoreSearchBounds()
        {
            Vector3 size = closedBounds.size;
            size.x += ignoreSearchPadding * 2f;
            size.z += ignoreSearchPadding * 2f;
            size.y += maxLiftHeight + ignoreSearchPadding * 2f;

            Vector3 center = closedBounds.center + Vector3.up * (maxLiftHeight * 0.5f);
            return new Bounds(center, size);
        }

        private void CacheClosedState()
        {
            Transform targetDoor = DoorTransform;
            closedPosition = targetDoor.position;

            Collider targetCollider = targetDoor.GetComponent<Collider>();
            closedBounds = targetCollider != null ? targetCollider.bounds : new Bounds(targetDoor.position, Vector3.one);
        }

        private void EnsureRigidbodySetup()
        {
            doorBody = door != null ? door.GetComponent<Rigidbody>() : null;
            if (doorBody == null && ensureKinematicRigidbody && door != null)
            {
                doorBody = door.gameObject.AddComponent<Rigidbody>();
            }

            if (doorBody == null)
            {
                return;
            }

            doorBody.isKinematic = true;
            doorBody.useGravity = false;
            doorBody.interpolation = RigidbodyInterpolation.Interpolate;
            doorBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            doorBody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void EnsureBuffers()
        {
            maxDetectedColliders = Mathf.Max(1, maxDetectedColliders);
            maxIgnoredColliders = Mathf.Max(1, maxIgnoredColliders);

            if (iceBuffer == null || iceBuffer.Length != maxDetectedColliders)
            {
                iceBuffer = new Collider[maxDetectedColliders];
            }

            if (ignoreBuffer == null || ignoreBuffer.Length != maxIgnoredColliders)
            {
                ignoreBuffer = new Collider[maxIgnoredColliders];
            }
        }

        private void ReportLiftState(bool isLifted)
        {
            if (wasLifted == isLifted)
            {
                return;
            }

            wasLifted = isLifted;
            if (logStateChanges)
            {
                Debug.Log($"{name} ice lift door {(isLifted ? "opened" : "closed")}.", this);
            }
        }

        private void OnValidate()
        {
            maxLiftHeight = Mathf.Max(0f, maxLiftHeight);
            extraLiftClearance = Mathf.Max(0f, extraLiftClearance);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            arriveDistance = Mathf.Max(0.001f, arriveDistance);
            easeSharpness = Mathf.Max(0.01f, easeSharpness);
            supportDepthBelowDoor = Mathf.Max(0.01f, supportDepthBelowDoor);
            footprintPadding = Mathf.Max(0f, footprintPadding);
            ignoreSearchPadding = Mathf.Max(0f, ignoreSearchPadding);
            EnsureBuffers();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos)
            {
                return;
            }

            CacheClosedState();

            Gizmos.color = closedColor;
            Gizmos.DrawWireCube(closedBounds.center, closedBounds.size);

            Bounds openBounds = closedBounds;
            openBounds.center += Vector3.up * maxLiftHeight;
            Gizmos.color = openColor;
            Gizmos.DrawWireCube(openBounds.center, openBounds.size);
            Gizmos.DrawLine(closedBounds.center, openBounds.center);

            Bounds scanBounds = GetIceScanBounds();
            Gizmos.color = scanColor;
            Gizmos.DrawCube(scanBounds.center, scanBounds.size);
            Gizmos.DrawWireCube(scanBounds.center, scanBounds.size);
        }
    }
}
