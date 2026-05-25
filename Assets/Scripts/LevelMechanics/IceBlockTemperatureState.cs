using UnityEngine;
using LeiHuo.Gameplay.TemperatureField;

namespace LeiHuo.Gameplay.LevelMechanics
{
    public class IceBlockTemperatureState : MonoBehaviour, ITemperatureFieldAffectable
    {
        public enum MeltMode
        {
            ShrinkThenDestroy,
            DestroyInstantly,
            MeltToWater
        }

        [Header("Melt Timing")]
        [SerializeField, Min(0f)] private float meltDelayAfterLeavingField = 1.5f;
        [HideInInspector]
        [SerializeField] private MeltMode meltMode = MeltMode.MeltToWater;
        [HideInInspector]
        [SerializeField, Min(0.01f)] private float shrinkSpeed = 1f;
        [HideInInspector]
        [SerializeField, Min(0f)] private float minimumScaleBeforeDestroy = 0.05f;

        [Header("Enhanced Ice")]
        [HideInInspector]
        [SerializeField] private bool useEnhancedMeltSettings;
        [HideInInspector]
        [SerializeField, Min(0f)] private float enhancedMeltDelayAfterLeavingField = 4.5f;
        [HideInInspector]
        [SerializeField] private MeltMode enhancedMeltMode = MeltMode.MeltToWater;
        [HideInInspector]
        [SerializeField, Min(0.01f)] private float enhancedShrinkSpeed = 0.5f;
        [HideInInspector]
        [SerializeField, Min(0f)] private float enhancedMinimumScaleBeforeDestroy = 0.05f;
        [SerializeField] private bool logEnhancedFieldHits;

        [Header("Material")]
        [SerializeField] private Material enhancedIceMaterial;
        [SerializeField] private Material waterMaterial;
        [SerializeField] private bool applyEnhancedMaterial = true;
        [SerializeField] private bool applyWaterMaterialOnMelt = true;
        [SerializeField] private bool restoreOriginalMaterialWhenRefrozen = true;

        [Header("Melt To Water Visual")]
        [SerializeField, Min(0.01f)] private float meltToWaterDuration = 1.2f;
        [SerializeField, Min(0.01f)] private float waterSpreadMultiplier = 1.6f;
        [SerializeField, Min(0.001f)] private float waterThicknessMultiplier = 0.08f;
        [SerializeField] private bool keepBottomAnchored = true;
        [SerializeField] private AnimationCurve meltToWaterCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool disableColliderWhenWater = true;
        [SerializeField, Min(0f)] private float waterLifetime = 2.5f;
        [SerializeField] private bool destroyWaterAfterLifetime = true;
        [SerializeField] private bool showWaterPreviewGizmo = true;
        [SerializeField] private Color waterPreviewColor = new Color(0.2f, 0.75f, 1f, 0.35f);

        [Header("Behaviour")]
        [SerializeField] private bool resetScaleWhenRefrozen = true;
        [SerializeField] private bool destroyWhenMelted = true;
        [SerializeField] private bool logStateChanges;

        private Vector3 originalScale;
        private float timeWithoutField;
        private bool isInsideTemperatureField;
        private bool isMelting;
        private bool hasBeenInsideTemperatureField;
        private bool shouldUseEnhancedMeltSettingsAfterExit;
        private bool isEnhancedIce;
        private bool hasCachedOriginalMaterials;
        private Renderer[] cachedRenderers;
        private Material[] originalMaterials;
        private Collider[] cachedColliders;
        private Vector3 meltStartScale;
        private Vector3 meltTargetWaterScale;
        private Vector3 meltStartPosition;
        private float meltTimer;
        private float waterTimer;
        private bool isWater;
        private bool hasReportedMeltComplete;
        private System.Action<IceBlockTemperatureState> meltedCallback;

        public bool IsInsideTemperatureField => isInsideTemperatureField;
        public bool IsMelting => isMelting;
        public bool IsUsingEnhancedMeltSettings => false;
        public float ActiveMeltDelayAfterLeavingField => meltDelayAfterLeavingField;
        public MeltMode ActiveMeltMode => IsUsingEnhancedMeltSettings ? enhancedMeltMode : meltMode;
        public float ActiveShrinkSpeed => IsUsingEnhancedMeltSettings ? enhancedShrinkSpeed : shrinkSpeed;
        public float ActiveMinimumScaleBeforeDestroy => IsUsingEnhancedMeltSettings ? enhancedMinimumScaleBeforeDestroy : minimumScaleBeforeDestroy;

        public float MeltDelayAfterLeavingField
        {
            get => meltDelayAfterLeavingField;
            set => meltDelayAfterLeavingField = Mathf.Max(0f, value);
        }

        public MeltMode CurrentMeltMode
        {
            get => meltMode;
            set => meltMode = value;
        }

        public float ShrinkSpeed
        {
            get => shrinkSpeed;
            set => shrinkSpeed = Mathf.Max(0.01f, value);
        }

        public float MinimumScaleBeforeDestroy
        {
            get => minimumScaleBeforeDestroy;
            set => minimumScaleBeforeDestroy = Mathf.Max(0f, value);
        }

        public float EnhancedMeltDelayAfterLeavingField
        {
            get => enhancedMeltDelayAfterLeavingField;
            set => enhancedMeltDelayAfterLeavingField = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (originalScale == Vector3.zero)
            {
                originalScale = transform.localScale;
            }
        }

        private void Update()
        {
            if (!hasBeenInsideTemperatureField || isInsideTemperatureField)
            {
                return;
            }

            timeWithoutField += Time.deltaTime;
            if (!isMelting && timeWithoutField >= ActiveMeltDelayAfterLeavingField)
            {
                BeginMelting();
            }

            if (isMelting)
            {
                TickMeltToWater();
            }
        }

        public void Initialize(
            Vector3 scale,
            float meltDelay,
            MeltMode mode,
            float meltShrinkSpeed,
            float minimumScale,
            bool shouldDestroyWhenMelted,
            bool shouldLogStateChanges,
            System.Action<IceBlockTemperatureState> onMelted)
        {
            transform.localScale = scale;
            originalScale = scale;
            meltDelayAfterLeavingField = Mathf.Max(0f, meltDelay);
            meltMode = mode;
            shrinkSpeed = Mathf.Max(0.01f, meltShrinkSpeed);
            minimumScaleBeforeDestroy = Mathf.Max(0f, minimumScale);
            destroyWhenMelted = shouldDestroyWhenMelted;
            logStateChanges = shouldLogStateChanges;
            meltedCallback = onMelted;
            MarkInsideTemperatureField();
        }

        public void ConfigureMaterials(Material enhancedMaterial, Material meltWaterMaterial)
        {
            if (enhancedMaterial != null)
            {
                enhancedIceMaterial = enhancedMaterial;
            }

            if (meltWaterMaterial != null)
            {
                waterMaterial = meltWaterMaterial;
            }

            ApplyCurrentIceMaterial();
        }

        public void MarkInsideTemperatureField()
        {
            MarkInsideTemperatureField(false);
        }

        public void MarkInsideTemperatureField(bool isEnhancedField)
        {
            bool wasEnhancedIce = isEnhancedIce;

            hasBeenInsideTemperatureField = true;
            isInsideTemperatureField = true;
            timeWithoutField = 0f;
            isMelting = false;
            shouldUseEnhancedMeltSettingsAfterExit = useEnhancedMeltSettings && isEnhancedField;
            isEnhancedIce = isEnhancedIce || isEnhancedField;
            isWater = false;
            hasReportedMeltComplete = false;
            meltTimer = 0f;
            waterTimer = 0f;

            if (logEnhancedFieldHits && isEnhancedIce && !wasEnhancedIce)
            {
                Debug.Log($"{name} became enhanced ice.", this);
            }

            if (resetScaleWhenRefrozen)
            {
                transform.localScale = originalScale;
            }

            RestoreColliders();
            ApplyCurrentIceMaterial();
        }

        public void MarkOutsideTemperatureField()
        {
            isInsideTemperatureField = false;
            timeWithoutField = 0f;
        }

        public void OnEnterTemperatureField(TemperatureFieldContext context)
        {
            MarkInsideTemperatureField(context.IsEnhanced);
        }

        public void OnStayTemperatureField(TemperatureFieldContext context)
        {
            MarkInsideTemperatureField(context.IsEnhanced);
        }

        public void OnExitTemperatureField(TemperatureFieldContext context)
        {
            MarkOutsideTemperatureField();
        }

        private void BeginMelting()
        {
            isMelting = true;

            if (logStateChanges)
            {
                Debug.Log($"{name} started melting.", this);
            }

            BeginMeltToWater();
        }

        private void TickShrinkMelt()
        {
            float step = ActiveShrinkSpeed * Time.deltaTime;
            transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.zero, step);

            if (transform.localScale.magnitude <= ActiveMinimumScaleBeforeDestroy)
            {
                FinishMelt();
            }
        }

        private void BeginMeltToWater()
        {
            CacheRenderersIfNeeded();
            CacheCollidersIfNeeded();

            meltTimer = 0f;
            waterTimer = 0f;
            isWater = false;
            meltStartScale = transform.localScale;
            meltStartPosition = transform.position;
            meltTargetWaterScale = new Vector3(
                originalScale.x * waterSpreadMultiplier,
                Mathf.Max(0.001f, originalScale.y * waterThicknessMultiplier),
                originalScale.z * waterSpreadMultiplier);

            if (applyWaterMaterialOnMelt && waterMaterial != null)
            {
                ApplyMaterial(waterMaterial);
            }

            if (disableColliderWhenWater)
            {
                SetCollidersEnabled(false);
            }
        }

        private void TickMeltToWater()
        {
            meltTimer += Time.deltaTime;
            float percent = Mathf.Clamp01(meltTimer / meltToWaterDuration);
            float curvedPercent = meltToWaterCurve != null ? Mathf.Clamp01(meltToWaterCurve.Evaluate(percent)) : percent;

            transform.localScale = Vector3.LerpUnclamped(meltStartScale, meltTargetWaterScale, curvedPercent);

            if (keepBottomAnchored)
            {
                float halfHeightDifference = (meltStartScale.y - transform.localScale.y) * 0.5f;
                transform.position = meltStartPosition - Vector3.up * halfHeightDifference;
            }

            if (percent < 1f)
            {
                return;
            }

            isWater = true;
            ReportMeltComplete();
            if (destroyWaterAfterLifetime)
            {
                waterTimer += Time.deltaTime;
                if (waterTimer >= waterLifetime)
                {
                    FinishMelt();
                }
            }
        }

        private void FinishMelt()
        {
            ReportMeltComplete();

            if (logStateChanges)
            {
                Debug.Log($"{name} melted.", this);
            }

            if (destroyWhenMelted)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ReportMeltComplete()
        {
            if (hasReportedMeltComplete)
            {
                return;
            }

            hasReportedMeltComplete = true;
            meltedCallback?.Invoke(this);
        }

        private void CacheRenderersIfNeeded()
        {
            if (hasCachedOriginalMaterials)
            {
                return;
            }

            cachedRenderers = GetComponentsInChildren<Renderer>();
            originalMaterials = new Material[cachedRenderers.Length];
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                originalMaterials[i] = cachedRenderers[i] != null ? cachedRenderers[i].sharedMaterial : null;
            }

            hasCachedOriginalMaterials = true;
        }

        private void CacheCollidersIfNeeded()
        {
            if (cachedColliders != null)
            {
                return;
            }

            cachedColliders = GetComponentsInChildren<Collider>();
        }

        private void ApplyCurrentIceMaterial()
        {
            CacheRenderersIfNeeded();

            if (applyEnhancedMaterial && isEnhancedIce && enhancedIceMaterial != null)
            {
                ApplyMaterial(enhancedIceMaterial);
                return;
            }

            if (restoreOriginalMaterialWhenRefrozen)
            {
                RestoreOriginalMaterials();
            }
        }

        private void ApplyMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            CacheRenderersIfNeeded();
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null)
                {
                    cachedRenderers[i].sharedMaterial = material;
                }
            }
        }

        private void RestoreOriginalMaterials()
        {
            if (!hasCachedOriginalMaterials || originalMaterials == null)
            {
                return;
            }

            for (int i = 0; i < cachedRenderers.Length && i < originalMaterials.Length; i++)
            {
                if (cachedRenderers[i] != null)
                {
                    cachedRenderers[i].sharedMaterial = originalMaterials[i];
                }
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            CacheCollidersIfNeeded();
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] != null)
                {
                    cachedColliders[i].enabled = enabled;
                }
            }
        }

        private void RestoreColliders()
        {
            SetCollidersEnabled(true);
        }

        private void OnValidate()
        {
            meltDelayAfterLeavingField = Mathf.Max(0f, meltDelayAfterLeavingField);
            shrinkSpeed = Mathf.Max(0.01f, shrinkSpeed);
            minimumScaleBeforeDestroy = Mathf.Max(0f, minimumScaleBeforeDestroy);
            enhancedMeltDelayAfterLeavingField = Mathf.Max(0f, enhancedMeltDelayAfterLeavingField);
            enhancedShrinkSpeed = Mathf.Max(0.01f, enhancedShrinkSpeed);
            enhancedMinimumScaleBeforeDestroy = Mathf.Max(0f, enhancedMinimumScaleBeforeDestroy);
            meltToWaterDuration = Mathf.Max(0.01f, meltToWaterDuration);
            waterSpreadMultiplier = Mathf.Max(0.01f, waterSpreadMultiplier);
            waterThicknessMultiplier = Mathf.Max(0.001f, waterThicknessMultiplier);
            waterLifetime = Mathf.Max(0f, waterLifetime);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showWaterPreviewGizmo)
            {
                return;
            }

            Vector3 referenceScale = Application.isPlaying && originalScale != Vector3.zero ? originalScale : transform.localScale;
            Vector3 previewScale = new Vector3(
                referenceScale.x * waterSpreadMultiplier,
                Mathf.Max(0.001f, referenceScale.y * waterThicknessMultiplier),
                referenceScale.z * waterSpreadMultiplier);

            Vector3 previewPosition = transform.position;
            if (keepBottomAnchored)
            {
                previewPosition -= Vector3.up * ((referenceScale.y - previewScale.y) * 0.5f);
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(previewPosition, transform.rotation, Vector3.one);
            Gizmos.color = waterPreviewColor;
            Gizmos.DrawWireCube(Vector3.zero, previewScale);
            Gizmos.matrix = previousMatrix;
        }
    }
}
