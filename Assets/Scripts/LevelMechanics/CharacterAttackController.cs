using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LeiHuo.Gameplay.LevelMechanics
{
    [DisallowMultipleComponent]
    public class CharacterAttackController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode attackMouseButton = KeyCode.Mouse0;
        [SerializeField] private KeyCode blockedWhileHeldMouseButton = KeyCode.Mouse1;
        [SerializeField] private IceBlockCarryController iceBlockCarryController;

        [Header("Attack Range")]
        [SerializeField] private Transform rangeOrigin;
        [SerializeField, Min(0.1f)] private float attackRadius = 2f;
        [SerializeField] private Vector3 rangeOffset = new Vector3(0f, 0.8f, 0.8f);
        [SerializeField] private LayerMask attackableLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
        [SerializeField, Min(1)] private int maxHits = 32;
        [SerializeField] private bool ignoreControllerHierarchy = true;

        [Header("Debug Preview")]
        [SerializeField] private bool showRangeGizmo = true;
        [SerializeField] private Color rangeGizmoColor = new Color(1f, 0.45f, 0.2f, 0.25f);
        [SerializeField] private bool logAttacks;

        private readonly HashSet<Component> notifiedAttackables = new HashSet<Component>();
        private Collider[] overlapHits;
        private int attackId;

        private void Awake()
        {
            if (rangeOrigin == null)
            {
                rangeOrigin = transform;
            }

            if (iceBlockCarryController == null)
            {
                iceBlockCarryController = GetComponent<IceBlockCarryController>();
            }

            AllocateHits();
        }

        private void Update()
        {
            if (!WasKeyPressedThisFrame(attackMouseButton) || IsAttackBlockedByOtherInteraction())
            {
                return;
            }

            Attack();
        }

        public void Attack()
        {
            AllocateHits();
            notifiedAttackables.Clear();

            Vector3 center = GetAttackCenter();
            Vector3 direction = GetAttackDirection();
            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                attackRadius,
                overlapHits,
                attackableLayers,
                triggerInteraction);

            attackId++;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = overlapHits[i];
                if (hitCollider == null || ShouldIgnoreHit(hitCollider))
                {
                    continue;
                }

                NotifyAttackables(hitCollider, center, direction);
            }

            if (logAttacks)
            {
                Debug.Log($"{name} attacked. Hit colliders: {hitCount}.", this);
            }
        }

        private void NotifyAttackables(Collider hitCollider, Vector3 center, Vector3 direction)
        {
            NotifyAttackables(hitCollider.GetComponentsInParent<MonoBehaviour>(), hitCollider, center, direction);
            NotifyAttackables(hitCollider.GetComponentsInChildren<MonoBehaviour>(), hitCollider, center, direction);
        }

        private void NotifyAttackables(MonoBehaviour[] behaviours, Collider hitCollider, Vector3 center, Vector3 direction)
        {
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (!(behaviour is IAttackable attackable) || !notifiedAttackables.Add(behaviour))
                {
                    continue;
                }

                Transform targetTransform = behaviour.transform;
                AttackContext context = new AttackContext(
                    gameObject,
                    rangeOrigin,
                    center,
                    direction,
                    targetTransform.gameObject,
                    hitCollider,
                    Vector3.Distance(center, targetTransform.position),
                    attackId,
                    Time.time);

                attackable.OnAttacked(context);
            }
        }

        private bool IsAttackBlockedByOtherInteraction()
        {
            if (IsKeyHeld(blockedWhileHeldMouseButton))
            {
                return true;
            }

            return iceBlockCarryController != null && iceBlockCarryController.IsCarrying;
        }

        private bool ShouldIgnoreHit(Collider hitCollider)
        {
            return ignoreControllerHierarchy && hitCollider.transform.IsChildOf(transform);
        }

        private Vector3 GetAttackCenter()
        {
            Transform origin = rangeOrigin != null ? rangeOrigin : transform;
            return origin.TransformPoint(rangeOffset);
        }

        private Vector3 GetAttackDirection()
        {
            Transform origin = rangeOrigin != null ? rangeOrigin : transform;
            Vector3 forward = origin.forward;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
        }

        private void AllocateHits()
        {
            if (overlapHits == null || overlapHits.Length != maxHits)
            {
                overlapHits = new Collider[maxHits];
            }
        }

        private bool WasKeyPressedThisFrame(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                if (keyCode == KeyCode.Mouse0)
                {
                    return Mouse.current.leftButton.wasPressedThisFrame;
                }

                if (keyCode == KeyCode.Mouse1)
                {
                    return Mouse.current.rightButton.wasPressedThisFrame;
                }

                if (keyCode == KeyCode.Mouse2)
                {
                    return Mouse.current.middleButton.wasPressedThisFrame;
                }
            }
#endif

            return Input.GetKeyDown(keyCode);
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

        private void OnValidate()
        {
            attackRadius = Mathf.Max(0.1f, attackRadius);
            maxHits = Mathf.Max(1, maxHits);
            AllocateHits();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showRangeGizmo)
            {
                return;
            }

            Gizmos.color = rangeGizmoColor;
            Gizmos.DrawWireSphere(GetAttackCenter(), attackRadius);
        }
    }
}
