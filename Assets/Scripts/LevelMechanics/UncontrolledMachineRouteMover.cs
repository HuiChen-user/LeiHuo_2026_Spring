using System.Collections.Generic;
using UnityEngine;
using LeiHuo.Gameplay.TemperatureField;

namespace LeiHuo.Gameplay.LevelMechanics
{
    [DisallowMultipleComponent]
    public class UncontrolledMachineRouteMover : MonoBehaviour, ITemperatureFieldAffectable
    {
        public enum RouteMode
        {
            Loop,
            PingPong,
            StopAtEnd
        }

        [Header("Route")]
        [SerializeField] private Transform routeSpace;
        [SerializeField] private List<Vector3> routePoints = new List<Vector3>();
        [SerializeField] private RouteMode routeMode = RouteMode.Loop;
        [SerializeField, Min(0)] private int startPointIndex;
        [SerializeField, Min(0.01f)] private float arriveDistance = 0.08f;
        [SerializeField] private bool snapToStartOnPlay = true;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 8f;
        [SerializeField] private bool rotateToMoveDirection = true;
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [SerializeField] private bool useRigidbodyWhenAvailable = true;

        [Header("Temperature Stop")]
        [SerializeField, Min(0f)] private float stopDurationAfterLeavingField = 1.2f;
        [SerializeField] private bool resetHoldTimerWhileStayingInField = true;

        [Header("Runtime")]
        [SerializeField] private bool moveOnStart = true;
        [SerializeField] private bool logStateChanges;

        [Header("Debug Preview")]
        [SerializeField] private bool showRouteGizmos = true;
        [SerializeField] private Color routeColor = new Color(1f, 0.25f, 0.12f, 0.9f);
        [SerializeField] private Color stoppedColor = new Color(0.25f, 0.85f, 1f, 0.95f);
        [SerializeField, Min(0.05f)] private float routePointGizmoRadius = 0.2f;

        private Rigidbody body;
        private int targetPointIndex;
        private int direction = 1;
        private float stopHoldTimer;
        private float slowHoldTimer;
        private float currentSlowSpeedMultiplier = 1f;
        private bool isInsideTemperatureField;
        private bool isInsideColdZone;
        private bool isSlowedByHighTemperatureField;
        private bool isStoppedByTemperatureField;
        private bool isMoving;
        private bool hasReachedEnd;

        public IReadOnlyList<Vector3> RoutePoints => routePoints;
        public Transform RouteSpace => routeSpace;
        public RouteMode CurrentRouteMode => routeMode;
        public bool ShowRouteGizmos => showRouteGizmos;
        public Color RouteColor => routeColor;
        public Color StoppedColor => stoppedColor;
        public float RoutePointGizmoRadius => routePointGizmoRadius;
        public bool IsStoppedByTemperature => isStoppedByTemperatureField || stopHoldTimer > 0f;
        public bool IsStoppedByColdZone => isInsideColdZone;
        public bool IsSlowedByHighTemperatureField => isSlowedByHighTemperatureField || slowHoldTimer > 0f;
        public bool IsMoving => isMoving;

        private void Reset()
        {
            routeSpace = transform.parent;
            routePoints.Clear();
            routePoints.Add(WorldToRoutePoint(transform.position));
            routePoints.Add(WorldToRoutePoint(transform.position + transform.forward * 4f));
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            isMoving = moveOnStart;
            ClampSettings();
            InitializeRouteProgress();
        }

        private void OnEnable()
        {
            InitializeRouteProgress();
        }

        private void Update()
        {
            if (body != null && useRigidbodyWhenAvailable)
            {
                return;
            }

            TickMovement(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (body == null || !useRigidbodyWhenAvailable)
            {
                return;
            }

            TickMovement(Time.fixedDeltaTime);
        }

        public void StartMoving()
        {
            isMoving = true;
            hasReachedEnd = false;
        }

        public void StopMoving()
        {
            isMoving = false;
        }

        public void OnEnterTemperatureField(TemperatureFieldContext context)
        {
            ApplyTemperatureFieldEffect(context, true);

            if (logStateChanges)
            {
                Debug.Log($"{name} affected by temperature field.", this);
            }
        }

        public void OnStayTemperatureField(TemperatureFieldContext context)
        {
            ApplyTemperatureFieldEffect(context, resetHoldTimerWhileStayingInField);
        }

        public void OnExitTemperatureField(TemperatureFieldContext context)
        {
            isInsideTemperatureField = false;
            isStoppedByTemperatureField = false;
            isSlowedByHighTemperatureField = false;

            if (context.IsCasterInHighTemperatureZone)
            {
                if (context.IsEnhanced)
                {
                    stopHoldTimer = context.HotZoneEnhancedStopDuration;
                }
                else
                {
                    slowHoldTimer = context.HotZoneUncontrolledSlowDuration;
                    currentSlowSpeedMultiplier = Mathf.Clamp01(context.HotZoneUncontrolledSlowSpeedMultiplier);
                }
            }
            else
            {
                stopHoldTimer = stopDurationAfterLeavingField;
            }

            if (logStateChanges)
            {
                Debug.Log($"{name} left temperature field.", this);
            }
        }

        public Vector3 GetWorldPoint(int index)
        {
            if (routePoints == null || routePoints.Count == 0)
            {
                return transform.position;
            }

            int clampedIndex = Mathf.Clamp(index, 0, routePoints.Count - 1);
            return RoutePointToWorld(routePoints[clampedIndex]);
        }

        public void SetWorldPoint(int index, Vector3 worldPosition)
        {
            if (routePoints == null || index < 0 || index >= routePoints.Count)
            {
                return;
            }

            routePoints[index] = WorldToRoutePoint(worldPosition);
        }

        public void InsertWorldPointAfter(int index, Vector3 worldPosition)
        {
            if (routePoints == null)
            {
                routePoints = new List<Vector3>();
            }

            int insertIndex = Mathf.Clamp(index + 1, 0, routePoints.Count);
            routePoints.Insert(insertIndex, WorldToRoutePoint(worldPosition));
            ClampSettings();
        }

        public void RemovePointAt(int index)
        {
            if (routePoints == null || routePoints.Count <= 2 || index < 0 || index >= routePoints.Count)
            {
                return;
            }

            routePoints.RemoveAt(index);
            ClampSettings();
            InitializeRouteProgress();
        }

        private void TickMovement(float deltaTime)
        {
            isInsideColdZone = ColdTemperatureZone.TryGetZoneAtPosition(transform.position, out _);
            TickStopHoldTimer(deltaTime);
            TickSlowHoldTimer(deltaTime);

            if (!CanMove() || deltaTime <= 0f)
            {
                return;
            }

            Vector3 currentPosition = body != null && useRigidbodyWhenAvailable ? body.position : transform.position;
            Vector3 targetPosition = GetWorldPoint(targetPointIndex);
            float maxDistanceDelta = moveSpeed * GetCurrentSpeedMultiplier() * deltaTime;
            Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, maxDistanceDelta);
            ApplyPosition(nextPosition);
            RotateTowardsMovement(nextPosition - currentPosition, deltaTime);

            if (Vector3.Distance(nextPosition, targetPosition) <= arriveDistance)
            {
                AdvanceTargetPoint();
            }
        }

        private void TickStopHoldTimer(float deltaTime)
        {
            if (isStoppedByTemperatureField || stopHoldTimer <= 0f || deltaTime <= 0f)
            {
                return;
            }

            stopHoldTimer = Mathf.Max(0f, stopHoldTimer - deltaTime);

            if (stopHoldTimer <= 0f && logStateChanges)
            {
                Debug.Log($"{name} resumed route movement.", this);
            }
        }

        private void TickSlowHoldTimer(float deltaTime)
        {
            if (isSlowedByHighTemperatureField || slowHoldTimer <= 0f || deltaTime <= 0f)
            {
                return;
            }

            slowHoldTimer = Mathf.Max(0f, slowHoldTimer - deltaTime);

            if (slowHoldTimer <= 0f)
            {
                currentSlowSpeedMultiplier = 1f;
                if (logStateChanges)
                {
                    Debug.Log($"{name} resumed full route speed.", this);
                }
            }
        }

        private bool CanMove()
        {
            return isMoving &&
                   !hasReachedEnd &&
                   moveSpeed > 0f &&
                   routePoints != null &&
                   routePoints.Count >= 2 &&
                   !isInsideColdZone &&
                   !IsStoppedByTemperature;
        }

        private float GetCurrentSpeedMultiplier()
        {
            return IsSlowedByHighTemperatureField ? Mathf.Clamp01(currentSlowSpeedMultiplier) : 1f;
        }

        private void ApplyTemperatureFieldEffect(TemperatureFieldContext context, bool refreshDuration)
        {
            isInsideTemperatureField = true;

            if (context.IsCasterInHighTemperatureZone)
            {
                if (context.IsEnhanced)
                {
                    isStoppedByTemperatureField = true;
                    isSlowedByHighTemperatureField = false;
                    slowHoldTimer = 0f;
                    currentSlowSpeedMultiplier = 1f;

                    if (refreshDuration)
                    {
                        stopHoldTimer = context.HotZoneEnhancedStopDuration;
                    }
                }
                else
                {
                    isStoppedByTemperatureField = false;
                    stopHoldTimer = 0f;
                    isSlowedByHighTemperatureField = true;
                    currentSlowSpeedMultiplier = Mathf.Clamp01(context.HotZoneUncontrolledSlowSpeedMultiplier);

                    if (refreshDuration)
                    {
                        slowHoldTimer = context.HotZoneUncontrolledSlowDuration;
                    }
                }

                return;
            }

            isStoppedByTemperatureField = true;
            isSlowedByHighTemperatureField = false;
            slowHoldTimer = 0f;
            currentSlowSpeedMultiplier = 1f;

            if (refreshDuration)
            {
                stopHoldTimer = stopDurationAfterLeavingField;
            }
        }

        private void ApplyPosition(Vector3 nextPosition)
        {
            if (body != null && useRigidbodyWhenAvailable)
            {
                body.MovePosition(nextPosition);
                return;
            }

            transform.position = nextPosition;
        }

        private void RotateTowardsMovement(Vector3 movementDelta, float deltaTime)
        {
            if (!rotateToMoveDirection || movementDelta.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(movementDelta.normalized, Vector3.up);
            if (turnSpeed <= 0f)
            {
                ApplyRotation(targetRotation);
                return;
            }

            Quaternion nextRotation = Quaternion.RotateTowards(
                body != null && useRigidbodyWhenAvailable ? body.rotation : transform.rotation,
                targetRotation,
                turnSpeed * deltaTime);
            ApplyRotation(nextRotation);
        }

        private void ApplyRotation(Quaternion nextRotation)
        {
            if (body != null && useRigidbodyWhenAvailable)
            {
                body.MoveRotation(nextRotation);
                return;
            }

            transform.rotation = nextRotation;
        }

        private void InitializeRouteProgress()
        {
            if (routePoints == null || routePoints.Count == 0)
            {
                return;
            }

            direction = 1;
            startPointIndex = Mathf.Clamp(startPointIndex, 0, routePoints.Count - 1);
            targetPointIndex = GetNextIndex(startPointIndex, direction);
            hasReachedEnd = false;

            if (snapToStartOnPlay && Application.isPlaying)
            {
                Vector3 startPosition = GetWorldPoint(startPointIndex);
                if (body != null && useRigidbodyWhenAvailable)
                {
                    body.position = startPosition;
                }
                else
                {
                    transform.position = startPosition;
                }
            }
        }

        private void AdvanceTargetPoint()
        {
            if (routePoints == null || routePoints.Count < 2)
            {
                return;
            }

            int nextIndex = GetNextIndex(targetPointIndex, direction);
            if (nextIndex == targetPointIndex)
            {
                hasReachedEnd = true;
                return;
            }

            targetPointIndex = nextIndex;
        }

        private int GetNextIndex(int currentIndex, int currentDirection)
        {
            int count = routePoints == null ? 0 : routePoints.Count;
            if (count <= 1)
            {
                return currentIndex;
            }

            int nextIndex = currentIndex + currentDirection;
            if (nextIndex >= 0 && nextIndex < count)
            {
                return nextIndex;
            }

            if (routeMode == RouteMode.Loop)
            {
                return currentDirection > 0 ? 0 : count - 1;
            }

            if (routeMode == RouteMode.PingPong)
            {
                direction *= -1;
                return Mathf.Clamp(currentIndex + direction, 0, count - 1);
            }

            return currentIndex;
        }

        private Vector3 RoutePointToWorld(Vector3 routePoint)
        {
            return routeSpace != null ? routeSpace.TransformPoint(routePoint) : routePoint;
        }

        private Vector3 WorldToRoutePoint(Vector3 worldPoint)
        {
            return routeSpace != null ? routeSpace.InverseTransformPoint(worldPoint) : worldPoint;
        }

        private void OnValidate()
        {
            ClampSettings();
        }

        private void ClampSettings()
        {
            if (routePoints == null)
            {
                routePoints = new List<Vector3>();
            }

            if (routeSpace == transform)
            {
                routeSpace = transform.parent;
            }

            moveSpeed = Mathf.Max(0f, moveSpeed);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            arriveDistance = Mathf.Max(0.01f, arriveDistance);
            stopDurationAfterLeavingField = Mathf.Max(0f, stopDurationAfterLeavingField);
            routePointGizmoRadius = Mathf.Max(0.05f, routePointGizmoRadius);

            if (routePoints.Count > 0)
            {
                startPointIndex = Mathf.Clamp(startPointIndex, 0, routePoints.Count - 1);
            }
            else
            {
                startPointIndex = 0;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showRouteGizmos || routePoints == null || routePoints.Count == 0)
            {
                return;
            }

            Color pointColor = IsStoppedByTemperature ? stoppedColor : routeColor;
            Gizmos.color = pointColor;

            for (int i = 0; i < routePoints.Count; i++)
            {
                Vector3 point = GetWorldPoint(i);
                Gizmos.DrawSphere(point, routePointGizmoRadius);

                if (i < routePoints.Count - 1)
                {
                    Gizmos.DrawLine(point, GetWorldPoint(i + 1));
                }
            }

            if (routeMode == RouteMode.Loop && routePoints.Count > 2)
            {
                Gizmos.DrawLine(GetWorldPoint(routePoints.Count - 1), GetWorldPoint(0));
            }
        }
    }
}
