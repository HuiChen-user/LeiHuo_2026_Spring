using UnityEngine;
using LeiHuo.Gameplay.TemperatureField;

namespace LeiHuo.Gameplay.LevelMechanics
{
    [DisallowMultipleComponent]
    public class CondensableWaterVapor : MonoBehaviour, ITemperatureFieldAffectable
    {
        private enum VaporTriggerShape
        {
            KeepExistingCollider,
            Sphere,
            Box
        }

        [Header("Vapor Trigger")]
        [SerializeField] private bool ensureVaporTrigger = true;
        [SerializeField] private VaporTriggerShape vaporTriggerShape = VaporTriggerShape.Sphere;
        [SerializeField, Min(0.01f)] private float vaporTriggerRadius = 0.75f;
        [SerializeField] private Vector3 vaporTriggerSize = Vector3.one;
        [SerializeField] private Vector3 vaporTriggerCenter;

        [Header("Ice Spawn")]
        [SerializeField] private GameObject icePrefab;
        [SerializeField] private Vector3 iceLocalOffset;
        [SerializeField] private Vector3 iceSize = Vector3.one;
        [SerializeField] private Vector3 iceEulerRotation;
        [SerializeField] private Material fallbackIceMaterial;
        [SerializeField] private Material enhancedIceMaterial;
        [SerializeField] private Material waterMaterial;
        [SerializeField] private bool parentIceToVapor;
        [SerializeField] private bool usePrefabScale;

        [Header("Ice Collider")]
        [SerializeField] private bool ensureIceCollider = true;
        [SerializeField] private bool iceColliderIsTrigger;
        [SerializeField] private Vector3 iceColliderSize = Vector3.one;

        [Header("Ice Carry")]
        [SerializeField] private bool ensureCarryableIce = true;

        [Header("Melt")]
        [SerializeField, Min(0f)] private float meltDelayAfterLeavingField = 1.5f;
        [HideInInspector]
        [SerializeField] private IceBlockTemperatureState.MeltMode meltMode = IceBlockTemperatureState.MeltMode.MeltToWater;
        [HideInInspector]
        [SerializeField, Min(0.01f)] private float shrinkSpeed = 1f;
        [HideInInspector]
        [SerializeField, Min(0f)] private float minimumScaleBeforeDestroy = 0.05f;

        [Header("Vapor Visual")]
        [SerializeField] private ParticleSystem vaporParticles;
        [SerializeField] private Renderer[] vaporRenderers;
        [SerializeField] private bool hideVaporWhileFrozen = true;
        [SerializeField] private bool stopParticlesWithClear = true;
        [SerializeField] private bool restoreVaporAfterIceMelted = true;

        [Header("Debug Preview")]
        [SerializeField] private bool showPreviewGizmos = true;
        [SerializeField] private Color icePreviewColor = new Color(0.35f, 0.85f, 1f, 0.35f);
        [SerializeField] private Color triggerPreviewColor = new Color(0.8f, 1f, 1f, 0.18f);
        [SerializeField] private bool logStateChanges;

        private IceBlockTemperatureState activeIce;
        private bool isInsideTemperatureField;

        private Vector3 IceWorldPosition => transform.TransformPoint(iceLocalOffset);
        private Quaternion IceWorldRotation => transform.rotation * Quaternion.Euler(iceEulerRotation);

        public void OnEnterTemperatureField(TemperatureFieldContext context)
        {
            isInsideTemperatureField = true;
            Condense(context.IsEnhanced);
        }

        public void OnStayTemperatureField(TemperatureFieldContext context)
        {
            isInsideTemperatureField = true;

            if (activeIce == null)
            {
                Condense(context.IsEnhanced);
                return;
            }

            activeIce.MarkInsideTemperatureField(context.IsEnhanced);
        }

        public void OnExitTemperatureField(TemperatureFieldContext context)
        {
            isInsideTemperatureField = false;

            if (activeIce != null)
            {
                activeIce.MarkOutsideTemperatureField();
            }
        }

        private void Reset()
        {
            vaporParticles = GetComponentInChildren<ParticleSystem>();
            vaporRenderers = GetComponentsInChildren<Renderer>();
            ConfigureVaporTrigger();
        }

        private void Awake()
        {
            ConfigureVaporTrigger();
        }

        private void Condense(bool isEnhancedField)
        {
            if (activeIce != null)
            {
                activeIce.MarkInsideTemperatureField(isEnhancedField);
                return;
            }

            GameObject iceObject = CreateIceObject();
            activeIce = ConfigureIceObject(iceObject);
            activeIce.MarkInsideTemperatureField(isEnhancedField);
            SetVaporVisible(!hideVaporWhileFrozen);

            if (logStateChanges)
            {
                Debug.Log($"{name} condensed into ice.", this);
            }
        }

        private GameObject CreateIceObject()
        {
            Transform parent = parentIceToVapor ? transform : null;
            GameObject iceObject;

            if (icePrefab != null)
            {
                iceObject = Instantiate(icePrefab, IceWorldPosition, IceWorldRotation, parent);
                if (!usePrefabScale)
                {
                    iceObject.transform.localScale = iceSize;
                }
            }
            else
            {
                iceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                iceObject.name = $"{name} Ice Block";
                iceObject.transform.SetPositionAndRotation(IceWorldPosition, IceWorldRotation);
                iceObject.transform.localScale = iceSize;
                if (parent != null)
                {
                    iceObject.transform.SetParent(parent, true);
                }

                ApplyFallbackMaterial(iceObject);
            }

            return iceObject;
        }

        private IceBlockTemperatureState ConfigureIceObject(GameObject iceObject)
        {
            if (ensureIceCollider)
            {
                ConfigureIceCollider(iceObject);
            }

            if (ensureCarryableIce && iceObject.GetComponent<CarryableIceBlock>() == null)
            {
                iceObject.AddComponent<CarryableIceBlock>();
            }

            IceBlockTemperatureState iceState = iceObject.GetComponent<IceBlockTemperatureState>();
            if (iceState == null)
            {
                iceState = iceObject.AddComponent<IceBlockTemperatureState>();
            }

            iceState.ConfigureMaterials(enhancedIceMaterial, waterMaterial);

            Vector3 finalScale = usePrefabScale && icePrefab != null ? iceObject.transform.localScale : iceSize;
            iceState.Initialize(
                finalScale,
                meltDelayAfterLeavingField,
                meltMode,
                shrinkSpeed,
                minimumScaleBeforeDestroy,
                true,
                logStateChanges,
                HandleIceMelted);

            return iceState;
        }

        private void ConfigureIceCollider(GameObject iceObject)
        {
            BoxCollider boxCollider = iceObject.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = iceObject.AddComponent<BoxCollider>();
            }

            boxCollider.isTrigger = iceColliderIsTrigger;
            boxCollider.size = iceColliderSize;
        }

        private void ConfigureVaporTrigger()
        {
            if (!ensureVaporTrigger)
            {
                return;
            }

            if (vaporTriggerShape == VaporTriggerShape.KeepExistingCollider)
            {
                Collider existingCollider = GetComponent<Collider>();
                if (existingCollider != null)
                {
                    existingCollider.isTrigger = true;
                }

                return;
            }

            if (vaporTriggerShape == VaporTriggerShape.Sphere)
            {
                SphereCollider sphereCollider = GetComponent<SphereCollider>();
                if (sphereCollider == null)
                {
                    sphereCollider = gameObject.AddComponent<SphereCollider>();
                }

                sphereCollider.isTrigger = true;
                sphereCollider.radius = vaporTriggerRadius;
                sphereCollider.center = vaporTriggerCenter;
                return;
            }

            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider>();
            }

            boxCollider.isTrigger = true;
            boxCollider.size = vaporTriggerSize;
            boxCollider.center = vaporTriggerCenter;
        }

        private void ApplyFallbackMaterial(GameObject iceObject)
        {
            if (fallbackIceMaterial == null)
            {
                return;
            }

            Renderer renderer = iceObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = fallbackIceMaterial;
            }
        }

        private void HandleIceMelted(IceBlockTemperatureState meltedIce)
        {
            if (activeIce == meltedIce)
            {
                activeIce = null;
            }

            if (restoreVaporAfterIceMelted && !isInsideTemperatureField)
            {
                SetVaporVisible(true);
            }
        }

        private void SetVaporVisible(bool visible)
        {
            if (vaporParticles != null)
            {
                if (visible)
                {
                    vaporParticles.Play();
                }
                else
                {
                    vaporParticles.Stop(true, stopParticlesWithClear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (vaporRenderers == null)
            {
                return;
            }

            for (int i = 0; i < vaporRenderers.Length; i++)
            {
                if (vaporRenderers[i] != null)
                {
                    vaporRenderers[i].enabled = visible;
                }
            }
        }

        private void OnValidate()
        {
            iceSize = ClampVector3(iceSize, 0.01f);
            iceColliderSize = ClampVector3(iceColliderSize, 0.01f);
            vaporTriggerRadius = Mathf.Max(0.01f, vaporTriggerRadius);
            vaporTriggerSize = ClampVector3(vaporTriggerSize, 0.01f);
            meltDelayAfterLeavingField = Mathf.Max(0f, meltDelayAfterLeavingField);
            shrinkSpeed = Mathf.Max(0.01f, shrinkSpeed);
            minimumScaleBeforeDestroy = Mathf.Max(0f, minimumScaleBeforeDestroy);
        }

        private Vector3 ClampVector3(Vector3 value, float minimum)
        {
            return new Vector3(
                Mathf.Max(minimum, value.x),
                Mathf.Max(minimum, value.y),
                Mathf.Max(minimum, value.z));
        }

        private void OnDrawGizmosSelected()
        {
            if (!showPreviewGizmos)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(IceWorldPosition, IceWorldRotation, Vector3.one);

            Gizmos.color = icePreviewColor;
            Gizmos.DrawCube(Vector3.zero, iceSize);
            Gizmos.DrawWireCube(Vector3.zero, iceSize);

            if (ensureIceCollider)
            {
                Gizmos.color = triggerPreviewColor;
                Gizmos.DrawWireCube(Vector3.zero, Vector3.Scale(iceSize, iceColliderSize));
            }

            Gizmos.matrix = previousMatrix;

            DrawVaporTriggerGizmo();
        }

        private void DrawVaporTriggerGizmo()
        {
            if (!ensureVaporTrigger || vaporTriggerShape == VaporTriggerShape.KeepExistingCollider)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = triggerPreviewColor;

            if (vaporTriggerShape == VaporTriggerShape.Sphere)
            {
                Gizmos.DrawWireSphere(vaporTriggerCenter, vaporTriggerRadius);
            }
            else
            {
                Gizmos.DrawWireCube(vaporTriggerCenter, vaporTriggerSize);
            }

            Gizmos.matrix = previousMatrix;
        }
    }
}
