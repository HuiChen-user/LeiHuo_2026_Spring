using UnityEngine;
using LeiHuo.Gameplay.TemperatureField;

namespace LeiHuo.Gameplay.LevelMechanics
{
    [DisallowMultipleComponent]
    public class TemperatureFieldMaterialSwitcher : MonoBehaviour, ITemperatureFieldAffectable
    {
        private enum MaterialApplyMode
        {
            ReplaceMatchingMaterialA,
            ReplaceAllSlots,
            ReplaceMaterialIndex
        }

        [Header("Materials")]
        [SerializeField] private Material materialA;
        [SerializeField] private Material materialB;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private MaterialApplyMode applyMode = MaterialApplyMode.ReplaceMatchingMaterialA;
        [SerializeField, Min(0)] private int materialIndex;

        [Header("Temperature Field")]
        [SerializeField] private bool stayChangedAfterLeavingField = true;
        [SerializeField] private bool requireEnhancedFieldWhenCasterInHighTemperatureZone = true;

        [Header("Feedback")]
        [SerializeField] private GameObject changedStateObject;
        [SerializeField] private GameObject changeEffectPrefab;
        [SerializeField] private bool logStateChanges;

        private Material[][] originalMaterials;
        private bool isChanged;

        public bool IsChanged => isChanged;

        private void Reset()
        {
            targetRenderers = GetComponentsInChildren<Renderer>();
        }

        private void Awake()
        {
            CacheTargets();
            SetChangedStateObjectActive(false);
        }

        public void OnEnterTemperatureField(TemperatureFieldContext context)
        {
            TryChangeMaterial(context);
        }

        public void OnStayTemperatureField(TemperatureFieldContext context)
        {
            TryChangeMaterial(context);
        }

        public void OnExitTemperatureField(TemperatureFieldContext context)
        {
            if (!stayChangedAfterLeavingField)
            {
                RestoreOriginalMaterials();
            }
        }

        public void RestoreOriginalMaterials()
        {
            if (!isChanged || targetRenderers == null || originalMaterials == null)
            {
                return;
            }

            for (int i = 0; i < targetRenderers.Length && i < originalMaterials.Length; i++)
            {
                if (targetRenderers[i] != null && originalMaterials[i] != null)
                {
                    targetRenderers[i].sharedMaterials = originalMaterials[i];
                }
            }

            isChanged = false;
            SetChangedStateObjectActive(false);
        }

        private void TryChangeMaterial(TemperatureFieldContext context)
        {
            if (isChanged || materialB == null || !CanChangeFromContext(context))
            {
                return;
            }

            if (!ApplyMaterialB())
            {
                return;
            }

            isChanged = true;
            SetChangedStateObjectActive(true);
            SpawnEffect(changeEffectPrefab);

            if (logStateChanges)
            {
                Debug.Log($"{name} material changed by temperature field.", this);
            }
        }

        private bool CanChangeFromContext(TemperatureFieldContext context)
        {
            return !requireEnhancedFieldWhenCasterInHighTemperatureZone ||
                   !context.IsCasterInHighTemperatureZone ||
                   context.IsEnhanced;
        }

        private bool ApplyMaterialB()
        {
            CacheTargets();

            if (targetRenderers == null)
            {
                return false;
            }

            bool changedAnyMaterial = false;
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                Material[] nextMaterials = targetRenderer.sharedMaterials;
                if (nextMaterials == null || nextMaterials.Length == 0)
                {
                    continue;
                }

                if (ApplyMaterialBToRenderer(nextMaterials, i))
                {
                    targetRenderer.sharedMaterials = nextMaterials;
                    changedAnyMaterial = true;
                }
            }

            return changedAnyMaterial;
        }

        private bool ApplyMaterialBToRenderer(Material[] materials, int rendererIndex)
        {
            bool changedAnyMaterial = false;

            if (applyMode == MaterialApplyMode.ReplaceAllSlots)
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = materialB;
                    changedAnyMaterial = true;
                }

                return changedAnyMaterial;
            }

            if (applyMode == MaterialApplyMode.ReplaceMaterialIndex)
            {
                if (materialIndex < materials.Length && IsSourceMaterial(materials[materialIndex], rendererIndex, materialIndex))
                {
                    materials[materialIndex] = materialB;
                    changedAnyMaterial = true;
                }

                return changedAnyMaterial;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                if (IsSourceMaterial(materials[i], rendererIndex, i))
                {
                    materials[i] = materialB;
                    changedAnyMaterial = true;
                }
            }

            return changedAnyMaterial;
        }

        private bool IsSourceMaterial(Material currentMaterial, int rendererIndex, int slotIndex)
        {
            if (materialA != null)
            {
                return currentMaterial == materialA;
            }

            if (originalMaterials == null ||
                rendererIndex >= originalMaterials.Length ||
                originalMaterials[rendererIndex] == null ||
                slotIndex >= originalMaterials[rendererIndex].Length)
            {
                return false;
            }

            return currentMaterial == originalMaterials[rendererIndex][slotIndex];
        }

        private void CacheTargets()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>();
            }

            if (originalMaterials != null || targetRenderers == null)
            {
                return;
            }

            originalMaterials = new Material[targetRenderers.Length][];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                originalMaterials[i] = targetRenderers[i] != null ? targetRenderers[i].sharedMaterials : null;
            }
        }

        private void SetChangedStateObjectActive(bool active)
        {
            if (changedStateObject != null && changedStateObject.activeSelf != active)
            {
                changedStateObject.SetActive(active);
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
    }
}
