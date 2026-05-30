using UnityEngine;
using LeiHuo.Gameplay.TemperatureField;

namespace LeiHuo.Gameplay.LevelMechanics
{
    [DisallowMultipleComponent]
    public class HighTemperatureFieldFreezableBreakable : MonoBehaviour, ITemperatureFieldAffectable, IAttackable
    {
        [Header("Freeze")]
        [SerializeField] private Material frozenMaterial;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private GameObject frozenStateObject;
        [SerializeField] private GameObject freezeEffectPrefab;

        [Header("Break")]
        [SerializeField] private bool destroyWhenBroken = true;
        [SerializeField, Min(0f)] private float destroyDelay;
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
        }

        public void OnAttacked(AttackContext context)
        {
            if (isBroken || !isFrozen)
            {
                return;
            }

            Break();
        }

        private void TryFreezeFromField(TemperatureFieldContext context)
        {
            if (isBroken || !context.IsCasterInHighTemperatureZone)
            {
                return;
            }

            SetFrozen(true);
        }

        private void SetFrozen(bool frozen)
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
                Debug.Log($"{name} {(frozen ? "froze from a high-temperature field" : "returned to normal")}.", this);
            }
        }

        private void Break()
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

            SetRenderersEnabled(false);
            SetCollidersEnabled(false);

            if (logStateChanges)
            {
                Debug.Log($"{name} broke from a high-temperature field.", this);
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
