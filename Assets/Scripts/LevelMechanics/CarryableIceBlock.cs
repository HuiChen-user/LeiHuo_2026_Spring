using System.Collections;
using UnityEngine;

namespace LeiHuo.Gameplay.LevelMechanics
{
    [DisallowMultipleComponent]
    public class CarryableIceBlock : MonoBehaviour
    {
        [Header("Carry")]
        [SerializeField] private bool canBeCarried = true;
        [SerializeField, Min(0.01f)] private float positionFollowSpeed = 18f;
        [SerializeField, Min(0.01f)] private float rotationFollowSpeed = 12f;
        [SerializeField] private bool keepUprightWhileCarried = true;
        [SerializeField] private bool disableGravityWhileCarried = true;

        [Header("Release Physics")]
        [SerializeField] private bool addRigidbodyOnRelease = true;
        [SerializeField] private bool removeRuntimeRigidbodyWhenSettled = true;
        [SerializeField, Min(0.01f)] private float mass = 8f;
        [SerializeField, Min(0f)] private float drag = 0.2f;
        [SerializeField, Min(0f)] private float angularDrag = 0.05f;
        [SerializeField, Min(0f)] private float releaseVelocityMultiplier = 0.35f;
        [SerializeField, Min(0f)] private float maxReleaseVelocity = 8f;
        [SerializeField, Min(0f)] private float settleCheckDelay = 0.4f;
        [SerializeField, Min(0f)] private float settledSpeedThreshold = 0.08f;
        [SerializeField, Min(0f)] private float settledAngularSpeedThreshold = 0.08f;
        [SerializeField, Min(0.1f)] private float maxRuntimeRigidbodyLifetime = 6f;

        [Header("Debug")]
        [SerializeField] private bool logStateChanges;

        private Rigidbody body;
        private bool hadRigidbodyBeforeCarry;
        private bool createdRuntimeRigidbody;
        private bool originalUseGravity;
        private bool originalIsKinematic;
        private float originalMass;
        private float originalDrag;
        private float originalAngularDrag;
        private Vector3 previousPosition;
        private Vector3 smoothedCarryVelocity;
        private Coroutine settleRoutine;

        public bool CanBeCarried => canBeCarried;
        public bool IsCarried { get; private set; }
        public Vector3 CurrentCarryVelocity => smoothedCarryVelocity;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void OnDisable()
        {
            IsCarried = false;
        }

        public bool TryBeginCarry()
        {
            if (!canBeCarried || IsCarried)
            {
                return false;
            }

            body = GetComponent<Rigidbody>();
            hadRigidbodyBeforeCarry = body != null;
            createdRuntimeRigidbody = false;
            StopSettleRoutine();

            if (body != null)
            {
                CacheRigidbodyState(body);
                body.isKinematic = true;
                if (disableGravityWhileCarried)
                {
                    body.useGravity = false;
                }
            }

            previousPosition = transform.position;
            smoothedCarryVelocity = Vector3.zero;
            IsCarried = true;

            if (logStateChanges)
            {
                Debug.Log($"{name} carry started.", this);
            }

            return true;
        }

        public void CarryTo(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!IsCarried)
            {
                return;
            }

            float positionT = 1f - Mathf.Exp(-positionFollowSpeed * Time.deltaTime);
            Vector3 nextPosition = Vector3.Lerp(transform.position, targetPosition, positionT);
            smoothedCarryVelocity = (nextPosition - previousPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            previousPosition = nextPosition;
            transform.position = nextPosition;

            if (keepUprightWhileCarried)
            {
                Vector3 euler = targetRotation.eulerAngles;
                targetRotation = Quaternion.Euler(0f, euler.y, 0f);
            }

            float rotationT = 1f - Mathf.Exp(-rotationFollowSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationT);
        }

        public void EndCarry(Vector3 requestedReleaseVelocity)
        {
            if (!IsCarried)
            {
                return;
            }

            IsCarried = false;
            Vector3 releaseVelocity = Vector3.ClampMagnitude(
                requestedReleaseVelocity * releaseVelocityMultiplier,
                maxReleaseVelocity);

            if (addRigidbodyOnRelease || hadRigidbodyBeforeCarry)
            {
                Rigidbody releaseBody = EnsureReleaseRigidbody();
                releaseBody.isKinematic = false;
                releaseBody.useGravity = true;
                releaseBody.velocity = releaseVelocity;
                releaseBody.angularVelocity = Vector3.zero;

                if (removeRuntimeRigidbodyWhenSettled && createdRuntimeRigidbody)
                {
                    settleRoutine = StartCoroutine(RemoveRuntimeRigidbodyWhenSettled(releaseBody));
                }
            }
            else if (body != null)
            {
                RestoreOriginalRigidbodyState(body);
            }

            if (logStateChanges)
            {
                Debug.Log($"{name} carry released.", this);
            }
        }

        private Rigidbody EnsureReleaseRigidbody()
        {
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
                createdRuntimeRigidbody = true;
            }
            else
            {
                createdRuntimeRigidbody = !hadRigidbodyBeforeCarry;
            }

            body.mass = mass;
            body.drag = drag;
            body.angularDrag = angularDrag;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return body;
        }

        private IEnumerator RemoveRuntimeRigidbodyWhenSettled(Rigidbody runtimeBody)
        {
            float elapsed = 0f;
            if (settleCheckDelay > 0f)
            {
                yield return new WaitForSeconds(settleCheckDelay);
                elapsed += settleCheckDelay;
            }

            while (runtimeBody != null && elapsed < maxRuntimeRigidbodyLifetime)
            {
                bool isSettled =
                    runtimeBody.velocity.magnitude <= settledSpeedThreshold &&
                    runtimeBody.angularVelocity.magnitude <= settledAngularSpeedThreshold;

                if (isSettled || runtimeBody.IsSleeping())
                {
                    Destroy(runtimeBody);
                    body = null;
                    settleRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (runtimeBody != null)
            {
                Destroy(runtimeBody);
                body = null;
            }

            settleRoutine = null;
        }

        private void CacheRigidbodyState(Rigidbody targetBody)
        {
            originalUseGravity = targetBody.useGravity;
            originalIsKinematic = targetBody.isKinematic;
            originalMass = targetBody.mass;
            originalDrag = targetBody.drag;
            originalAngularDrag = targetBody.angularDrag;
        }

        private void RestoreOriginalRigidbodyState(Rigidbody targetBody)
        {
            targetBody.useGravity = originalUseGravity;
            targetBody.isKinematic = originalIsKinematic;
            targetBody.mass = originalMass;
            targetBody.drag = originalDrag;
            targetBody.angularDrag = originalAngularDrag;
        }

        private void StopSettleRoutine()
        {
            if (settleRoutine == null)
            {
                return;
            }

            StopCoroutine(settleRoutine);
            settleRoutine = null;
        }

        private void OnValidate()
        {
            positionFollowSpeed = Mathf.Max(0.01f, positionFollowSpeed);
            rotationFollowSpeed = Mathf.Max(0.01f, rotationFollowSpeed);
            mass = Mathf.Max(0.01f, mass);
            drag = Mathf.Max(0f, drag);
            angularDrag = Mathf.Max(0f, angularDrag);
            releaseVelocityMultiplier = Mathf.Max(0f, releaseVelocityMultiplier);
            maxReleaseVelocity = Mathf.Max(0f, maxReleaseVelocity);
            settleCheckDelay = Mathf.Max(0f, settleCheckDelay);
            settledSpeedThreshold = Mathf.Max(0f, settledSpeedThreshold);
            settledAngularSpeedThreshold = Mathf.Max(0f, settledAngularSpeedThreshold);
            maxRuntimeRigidbodyLifetime = Mathf.Max(0.1f, maxRuntimeRigidbodyLifetime);
        }
    }
}
