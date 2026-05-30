using UnityEngine;

namespace LeiHuo.Gameplay.LevelMechanics
{
    [DisallowMultipleComponent]
    public class DirectBreakableDoor : MonoBehaviour, IAttackable
    {
        [Header("Break")]
        [SerializeField] private GameObject targetObject;
        [SerializeField, Min(0f)] private float destroyDelay;
        [SerializeField] private bool destroyWhenBroken = true;
        [SerializeField] private bool disableRenderersWhenBroken = true;
        [SerializeField] private bool disableCollidersWhenBroken = true;
        [SerializeField] private GameObject breakEffectPrefab;
        [SerializeField] private AudioSource breakAudioSource;

        [Header("Debug")]
        [SerializeField] private bool logStateChanges;

        private Renderer[] targetRenderers;
        private Collider[] targetColliders;
        private bool isBroken;

        public bool IsBroken => isBroken;

        private GameObject TargetObject => targetObject != null ? targetObject : gameObject;

        private void Awake()
        {
            CacheTargets();
        }

        private void Reset()
        {
            targetObject = gameObject;
        }

        public void OnAttacked(AttackContext context)
        {
            Break();
        }

        public void Break()
        {
            if (isBroken)
            {
                return;
            }

            isBroken = true;

            SpawnEffect();
            if (breakAudioSource != null)
            {
                breakAudioSource.Play();
            }

            if (disableRenderersWhenBroken)
            {
                SetRenderersEnabled(false);
            }

            if (disableCollidersWhenBroken)
            {
                SetCollidersEnabled(false);
            }

            if (logStateChanges)
            {
                Debug.Log($"{TargetObject.name} was destroyed by an attack.", this);
            }

            if (destroyWhenBroken)
            {
                Destroy(TargetObject, destroyDelay);
            }
            else
            {
                TargetObject.SetActive(false);
            }
        }

        private void CacheTargets()
        {
            GameObject currentTarget = TargetObject;
            targetRenderers = currentTarget.GetComponentsInChildren<Renderer>();
            targetColliders = currentTarget.GetComponentsInChildren<Collider>();
        }

        private void SetRenderersEnabled(bool enabled)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null)
                {
                    targetRenderers[i].enabled = enabled;
                }
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                if (targetColliders[i] != null)
                {
                    targetColliders[i].enabled = enabled;
                }
            }
        }

        private void SpawnEffect()
        {
            if (breakEffectPrefab == null)
            {
                return;
            }

            Instantiate(breakEffectPrefab, TargetObject.transform.position, TargetObject.transform.rotation);
        }

        private void OnValidate()
        {
            destroyDelay = Mathf.Max(0f, destroyDelay);
        }
    }
}
