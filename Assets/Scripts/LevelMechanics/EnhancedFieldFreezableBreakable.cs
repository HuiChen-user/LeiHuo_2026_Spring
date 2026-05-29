using UnityEngine;
using LeiHuo.Gameplay.TemperatureField;

namespace LeiHuo.Gameplay.LevelMechanics
{
    [DisallowMultipleComponent]
    public class EnhancedFieldFreezableBreakable : MonoBehaviour, ITemperatureFieldAffectable, IAttackable
    {
        [Header("Freeze")]
        [SerializeField] private bool freezeOnEnhancedFieldEnter = true;
        [SerializeField] private bool stayFrozenAfterLeavingField = true;
        [SerializeField] private Material frozenMaterial;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private GameObject frozenStateObject;
        [SerializeField] private GameObject freezeEffectPrefab;

        [Header("Break")]
        [SerializeField] private bool canBreakOnlyWhenFrozen = true;
        [SerializeField] private bool destroyWhenBroken = true;
        [SerializeField, Min(0f)] private float destroyDelay;
        [SerializeField] private bool disableRenderersWhenBroken = true;
        [SerializeField] private bool disableCollidersWhenBroken = true;
        [SerializeField] private GameObject breakEffectPrefab;
        [SerializeField] private AudioSource breakAudioSource;

        [Header("Debug")]
        [SerializeField] private bool logStateChanges;

        private Material[][] originalMaterials;
        private Collider[] targetColliders;
        private bool isFrozen;
        private bool isBroken;

        public bool IsFrozen => isFrozen;
        public bool IsBroken => isBroken;

        private void Awake()
        {
            CacheTargets();
            SetFrozenVisual(false);
        }

        private void Reset()
        {
            targetRenderers = GetComponentsInChildren<Renderer>();
        }

        private void Update()
        {
            if (!isBroken && ColdTemperatureZone.TryGetZoneAtPosition(transform.position, out _))
            {
                SetFrozen(true);
            }
        }

        public void OnEnterTemperatureField(TemperatureFieldContext context)
        {
            TryFreezeFromField(context);
        }

        public void OnStayTemperatureField(TemperatureFieldContext context)
        {
            TryFreezeFromField(context);
        }

        public void OnExitTemperatureField(TemperatureFieldContext context)
        {
            if (!stayFrozenAfterLeavingField && !isBroken)
            {
                SetFrozen(false);
            }
        }

        public void OnAttacked(AttackContext context)
        {
            if (isBroken || (canBreakOnlyWhenFrozen && !isFrozen))
            {
                return;
            }

            Break();
        }

        public void SetFrozen(bool frozen)
        {
            if (isBroken || isFrozen == frozen)
            {
                return;
            }

            isFrozen = frozen;
            SetFrozenVisual(frozen);

            if (frozen)
            {
                SpawnEffect(freezeEffectPrefab);
            }

            if (logStateChanges)
            {
                Debug.Log($"{name} {(frozen ? "froze solid" : "returned to normal")}.", this);
            }
        }

        public void Break()
        {
            if (isBroken)
            {
                return;
            }

            isBroken = true;
            isFrozen = false;
            SetFrozenStateObjectActive(false);

            SpawnEffect(breakEffectPrefab);
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
                Debug.Log($"{name} broke after being attacked.", this);
            }

            if (destroyWhenBroken)
            {
                Destroy(gameObject, destroyDelay);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void TryFreezeFromField(TemperatureFieldContext context)
        {
            if (freezeOnEnhancedFieldEnter && (context.IsEnhanced || ColdTemperatureZone.TryGetZoneAtPosition(transform.position, out _)))
            {
                SetFrozen(true);
            }
        }

        private void CacheTargets()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>();
            }

            targetColliders = GetComponentsInChildren<Collider>();
            CacheOriginalMaterials();
        }

        private void CacheOriginalMaterials()
        {
            originalMaterials = new Material[targetRenderers.Length][];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                originalMaterials[i] = targetRenderers[i] != null ? targetRenderers[i].sharedMaterials : null;
            }
        }

        private void SetFrozenVisual(bool frozen)
        {
            SetFrozenStateObjectActive(frozen);

            if (frozenMaterial == null || targetRenderers == null)
            {
                return;
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.sharedMaterials = frozen
                    ? CreateMaterialArray(targetRenderer.sharedMaterials.Length, frozenMaterial)
                    : originalMaterials[i];
            }
        }

        private void SetFrozenStateObjectActive(bool active)
        {
            if (frozenStateObject != null && frozenStateObject.activeSelf != active)
            {
                frozenStateObject.SetActive(active);
            }
        }

        private Material[] CreateMaterialArray(int length, Material material)
        {
            int safeLength = Mathf.Max(1, length);
            Material[] materials = new Material[safeLength];
            for (int i = 0; i < safeLength; i++)
            {
                materials[i] = material;
            }

            return materials;
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (targetRenderers == null)
            {
                return;
            }

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
            if (targetColliders == null)
            {
                return;
            }

            for (int i = 0; i < targetColliders.Length; i++)
            {
                if (targetColliders[i] != null)
                {
                    targetColliders[i].enabled = enabled;
                }
            }
        }

        private void SpawnEffect(GameObject effectPrefab)
        {
            if (effectPrefab == null)
            {
                return;
            }

            Instantiate(effectPrefab, transform.position, transform.rotation);
        }

        private void OnValidate()
        {
            destroyDelay = Mathf.Max(0f, destroyDelay);
        }
    }
}
