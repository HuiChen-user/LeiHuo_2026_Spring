using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LeiHuo.Gameplay.TemperatureField
{
    public class TemperatureFieldController : MonoBehaviour
    {
        private enum FieldState
        {
            Idle,
            Expanding,
            HoldingAtMax,
            Fading
        }

        [Header("Input")]
        [SerializeField] private KeyCode activationKey = KeyCode.E;

        [Header("Shape")]
        [SerializeField] private Vector3 centerOffset = new Vector3(0f, 0.9f, 0f);
        [SerializeField, Min(0f)] private float initialRadius = 0.15f;
        [SerializeField, Min(0.01f)] private float maxRadius = 5f;
        [SerializeField, Min(0.01f)] private float expandSpeed = 4f;

        [Header("Enhancement")]
        [SerializeField, Min(1f)] private float enhancedRadiusMultiplier = 1.5f;
        [SerializeField, Min(1f)] private float enhancedStrengthMultiplier = 1.5f;
        [SerializeField] private Color enhancedFieldColor = new Color(1f, 0.55f, 0.12f, 0.34f);
        [SerializeField] private Color enhancedBoundaryColor = new Color(1f, 0.82f, 0.25f, 0.95f);
        [SerializeField] private Color enhancedGroundProjectionColor = new Color(1f, 0.58f, 0.15f, 0.95f);
        [SerializeField, Min(1f)] private float enhancedLineWidthMultiplier = 1.25f;
        [SerializeField] private bool logEnhancementChanges;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float maxRadiusHoldDuration = 0.6f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.25f;
        [SerializeField, Min(0f)] private float cooldownDuration = 0f;

        [Header("Detection")]
        [SerializeField] private LayerMask affectableLayers = ~0;
        [SerializeField, Min(0.01f)] private float detectionInterval = 0.05f;
        [SerializeField, Min(1)] private int maxDetectedColliders = 64;
        [SerializeField] private bool logDetectionChanges;

        [Header("Visual")]
        [SerializeField] private Color fieldColor = new Color(0.2f, 0.75f, 1f, 0.28f);
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.28f;
        [SerializeField] private bool showRuntimeSphere = true;
        [SerializeField] private bool showBoundaryRings = true;
        [SerializeField] private bool showGroundProjection = true;
        [SerializeField] private Color boundaryColor = new Color(0.6f, 0.95f, 1f, 0.9f);
        [SerializeField] private Color groundProjectionColor = new Color(0.15f, 0.9f, 1f, 0.95f);
        [SerializeField, Min(0.005f)] private float boundaryLineWidth = 0.04f;
        [SerializeField, Min(0.005f)] private float groundLineWidth = 0.07f;
        [SerializeField, Range(16, 192)] private int ringSegments = 96;
        [SerializeField] private float groundProjectionYOffset = 0.03f;
        [SerializeField] private bool showSceneGizmo = true;

        [Header("High Temperature UI")]
        [SerializeField] private bool showHighTemperatureScreenFrame = true;
        [SerializeField] private Color highTemperatureFrameColor = new Color(1f, 0.08f, 0.02f, 0.18f);
        [SerializeField, Min(1f)] private float highTemperatureFrameThickness = 18f;

        [Header("Cold Temperature UI")]
        [SerializeField] private bool showColdTemperatureScreenFrame = true;
        [SerializeField] private Color coldTemperatureFrameColor = new Color(0.08f, 0.45f, 1f, 0.18f);
        [SerializeField, Min(1f)] private float coldTemperatureFrameThickness = 18f;

        public float CurrentRadius => currentRadius;
        public bool IsActive => state != FieldState.Idle;
        public bool HasStoredEnhancement => hasStoredEnhancement;
        public bool IsCurrentFieldEnhanced => isCurrentFieldEnhanced;

        private readonly HashSet<ITemperatureFieldAffectable> affectedObjects = new HashSet<ITemperatureFieldAffectable>();
        private readonly HashSet<ITemperatureFieldAffectable> detectedThisTick = new HashSet<ITemperatureFieldAffectable>();
        private readonly List<ITemperatureFieldAffectable> exitedAffectedObjects = new List<ITemperatureFieldAffectable>();
        private readonly HashSet<Collider> detectedColliders = new HashSet<Collider>();
        private readonly HashSet<Collider> detectedCollidersThisTick = new HashSet<Collider>();
        private readonly List<Collider> exitedDetectedColliders = new List<Collider>();

        private Collider[] overlapBuffer;
        private FieldState state = FieldState.Idle;
        private GameObject visualSphere;
        private Renderer visualRenderer;
        private Material visualMaterial;
        private Transform visualRoot;
        private LineRenderer horizontalRing;
        private LineRenderer verticalForwardRing;
        private LineRenderer verticalSideRing;
        private LineRenderer groundProjectionRing;
        private Material ringMaterial;
        private float currentRadius;
        private float elapsedTime;
        private float holdTimer;
        private float fadeTimer;
        private float cooldownTimer;
        private float nextDetectionTime;
        private bool hasStoredEnhancement;
        private bool isCurrentFieldEnhanced;
        private bool currentEnhancedFieldAffectedObject;

        private void Awake()
        {
            AllocateOverlapBuffer();
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            bool isHeld = IsActivationHeld();

            if (state == FieldState.Idle && isHeld && cooldownTimer <= 0f)
            {
                BeginField();
            }

            if ((state == FieldState.Expanding || state == FieldState.HoldingAtMax) && !isHeld)
            {
                BeginFade();
            }

            TickField();
        }

        private void OnDisable()
        {
            ClearAffectedObjects();
            DestroyVisual();
            state = FieldState.Idle;
            isCurrentFieldEnhanced = false;
            currentEnhancedFieldAffectedObject = false;
        }

        private void OnValidate()
        {
            initialRadius = Mathf.Max(0f, initialRadius);
            maxRadius = Mathf.Max(0.01f, maxRadius);
            expandSpeed = Mathf.Max(0.01f, expandSpeed);
            enhancedRadiusMultiplier = Mathf.Max(1f, enhancedRadiusMultiplier);
            enhancedStrengthMultiplier = Mathf.Max(1f, enhancedStrengthMultiplier);
            enhancedLineWidthMultiplier = Mathf.Max(1f, enhancedLineWidthMultiplier);
            fadeDuration = Mathf.Max(0.01f, fadeDuration);
            detectionInterval = Mathf.Max(0.01f, detectionInterval);
            maxDetectedColliders = Mathf.Max(1, maxDetectedColliders);
            maxAlpha = Mathf.Clamp01(maxAlpha);
            boundaryLineWidth = Mathf.Max(0.005f, boundaryLineWidth);
            groundLineWidth = Mathf.Max(0.005f, groundLineWidth);
            ringSegments = Mathf.Clamp(ringSegments, 16, 192);
            highTemperatureFrameColor.a = Mathf.Clamp01(highTemperatureFrameColor.a);
            highTemperatureFrameThickness = Mathf.Max(1f, highTemperatureFrameThickness);
            coldTemperatureFrameColor.a = Mathf.Clamp01(coldTemperatureFrameColor.a);
            coldTemperatureFrameThickness = Mathf.Max(1f, coldTemperatureFrameThickness);

            if (initialRadius > maxRadius)
            {
                initialRadius = maxRadius;
            }

            if (overlapBuffer == null || overlapBuffer.Length != maxDetectedColliders)
            {
                AllocateOverlapBuffer();
            }
        }

        private bool IsActivationHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && TryGetInputSystemKey(activationKey, out Key key))
            {
                return Keyboard.current[key].isPressed;
            }
#endif

            return Input.GetKey(activationKey);
        }

#if ENABLE_INPUT_SYSTEM
        private bool TryGetInputSystemKey(KeyCode keyCode, out Key key)
        {
            try
            {
                key = (Key)Enum.Parse(typeof(Key), keyCode.ToString());
                return true;
            }
            catch (ArgumentException)
            {
                key = Key.None;
                return false;
            }
        }
#endif

        private void BeginField()
        {
            state = FieldState.Expanding;
            isCurrentFieldEnhanced = hasStoredEnhancement;
            currentEnhancedFieldAffectedObject = false;
            currentRadius = initialRadius;
            elapsedTime = 0f;
            holdTimer = 0f;
            fadeTimer = 0f;
            nextDetectionTime = 0f;
            CreateVisualIfNeeded();
            UpdateVisual(maxAlpha, 1f);

            if (logEnhancementChanges && isCurrentFieldEnhanced)
            {
                Debug.Log($"{name} released an enhanced temperature field.", this);
            }
        }

        private void TickField()
        {
            if (state == FieldState.Idle)
            {
                return;
            }

            elapsedTime += Time.deltaTime;

            if (state == FieldState.Expanding)
            {
                float targetRadius = GetCurrentMaxRadius();
                currentRadius = Mathf.Min(targetRadius, currentRadius + expandSpeed * Time.deltaTime);
                if (Mathf.Approximately(currentRadius, targetRadius))
                {
                    state = FieldState.HoldingAtMax;
                    holdTimer = 0f;
                }
            }
            else if (state == FieldState.HoldingAtMax)
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= maxRadiusHoldDuration)
                {
                    BeginFade();
                }
            }
            else if (state == FieldState.Fading)
            {
                fadeTimer += Time.deltaTime;
                float fadePercent = Mathf.Clamp01(fadeTimer / fadeDuration);
                float fadeAlphaMultiplier = 1f - fadePercent;
                UpdateVisual(Mathf.Lerp(maxAlpha, 0f, fadePercent), fadeAlphaMultiplier);

                if (fadePercent >= 1f)
                {
                    EndField();
                    return;
                }
            }

            if (state != FieldState.Fading)
            {
                UpdateVisual(maxAlpha, 1f);
            }

            if (Time.time >= nextDetectionTime)
            {
                DetectAffectables();
                nextDetectionTime = Time.time + detectionInterval;
            }
        }

        private void BeginFade()
        {
            if (state == FieldState.Idle || state == FieldState.Fading)
            {
                return;
            }

            state = FieldState.Fading;
            fadeTimer = 0f;
        }

        private void EndField()
        {
            ClearAffectedObjects();
            ConsumeEnhancementIfUsed();
            DestroyVisual();
            cooldownTimer = cooldownDuration;
            state = FieldState.Idle;
            currentRadius = 0f;
            isCurrentFieldEnhanced = false;
            currentEnhancedFieldAffectedObject = false;
        }

        public bool TryGrantEnhancement()
        {
            if (hasStoredEnhancement)
            {
                return false;
            }

            hasStoredEnhancement = true;

            if (logEnhancementChanges)
            {
                Debug.Log($"{name} gained a temperature field enhancement.", this);
            }

            return true;
        }

        public void GrantEnhancement()
        {
            if (!TryGrantEnhancement() && logEnhancementChanges)
            {
                Debug.Log($"{name} already has a stored temperature field enhancement.", this);
            }
        }

        private void DetectAffectables()
        {
            AllocateOverlapBuffer();
            detectedThisTick.Clear();
            detectedCollidersThisTick.Clear();

            Vector3 center = GetFieldCenter();
            int count = Physics.OverlapSphereNonAlloc(
                center,
                currentRadius,
                overlapBuffer,
                affectableLayers,
                QueryTriggerInteraction.Collide);

            TemperatureFieldContext context = CreateContext(center);

            for (int i = 0; i < count; i++)
            {
                Collider hit = overlapBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                TrackDetectedCollider(hit);

                ITemperatureFieldAffectable affectable = FindAffectable(hit);
                if (affectable == null)
                {
                    continue;
                }

                detectedThisTick.Add(affectable);

                if (affectedObjects.Add(affectable))
                {
                    affectable.OnEnterTemperatureField(context);
                    MarkEnhancedFieldAffectedObject();
                    if (logDetectionChanges)
                    {
                        Debug.Log($"{name} temperature field entered: {hit.name}", hit);
                    }
                }

                affectable.OnStayTemperatureField(context);
                MarkEnhancedFieldAffectedObject();
            }

            RemoveExitedAffectables(context);
            RemoveExitedDetectedColliders();
        }

        private void TrackDetectedCollider(Collider hit)
        {
            detectedCollidersThisTick.Add(hit);

            if (logDetectionChanges && detectedColliders.Add(hit))
            {
                Debug.Log($"{name} temperature field detected collider entered: {hit.name}", hit);
            }
        }

        private void RemoveExitedAffectables(TemperatureFieldContext context)
        {
            if (affectedObjects.Count == 0)
            {
                return;
            }

            exitedAffectedObjects.Clear();

            foreach (ITemperatureFieldAffectable affectable in affectedObjects)
            {
                if (!detectedThisTick.Contains(affectable))
                {
                    exitedAffectedObjects.Add(affectable);
                }
            }

            if (exitedAffectedObjects.Count == 0)
            {
                return;
            }

            foreach (ITemperatureFieldAffectable affectable in exitedAffectedObjects)
            {
                affectedObjects.Remove(affectable);
                affectable.OnExitTemperatureField(context);

                if (logDetectionChanges)
                {
                    Debug.Log($"{name} temperature field exited: {affectable}");
                }
            }
        }

        private void RemoveExitedDetectedColliders()
        {
            if (!logDetectionChanges || detectedColliders.Count == 0)
            {
                return;
            }

            exitedDetectedColliders.Clear();

            foreach (Collider detectedCollider in detectedColliders)
            {
                if (!detectedCollidersThisTick.Contains(detectedCollider))
                {
                    exitedDetectedColliders.Add(detectedCollider);
                }
            }

            foreach (Collider exitedCollider in exitedDetectedColliders)
            {
                detectedColliders.Remove(exitedCollider);
                if (exitedCollider != null)
                {
                    Debug.Log($"{name} temperature field detected collider exited: {exitedCollider.name}", exitedCollider);
                }
            }
        }

        private ITemperatureFieldAffectable FindAffectable(Collider hit)
        {
            MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ITemperatureFieldAffectable affectable)
                {
                    return affectable;
                }
            }

            return null;
        }

        private TemperatureFieldContext CreateContext(Vector3 center)
        {
            float targetRadius = GetCurrentMaxRadius();
            float normalizedRadius = targetRadius <= 0f ? 0f : Mathf.Clamp01(currentRadius / targetRadius);
            float strengthMultiplier = isCurrentFieldEnhanced ? enhancedStrengthMultiplier : 1f;
            HighTemperatureZone.TryGetZoneAtPosition(transform.position, out HighTemperatureZone highTemperatureZone);

            return new TemperatureFieldContext(
                gameObject,
                transform,
                center,
                currentRadius,
                normalizedRadius,
                elapsedTime,
                isCurrentFieldEnhanced,
                strengthMultiplier,
                highTemperatureZone);
        }

        private float GetCurrentMaxRadius()
        {
            return isCurrentFieldEnhanced ? maxRadius * enhancedRadiusMultiplier : maxRadius;
        }

        private void MarkEnhancedFieldAffectedObject()
        {
            if (isCurrentFieldEnhanced)
            {
                currentEnhancedFieldAffectedObject = true;
            }
        }

        private void ConsumeEnhancementIfUsed()
        {
            if (!isCurrentFieldEnhanced || !currentEnhancedFieldAffectedObject)
            {
                return;
            }

            hasStoredEnhancement = false;

            if (logEnhancementChanges)
            {
                Debug.Log($"{name} consumed the stored temperature field enhancement.", this);
            }
        }

        private void ClearAffectedObjects()
        {
            if (affectedObjects.Count == 0)
            {
                return;
            }

            TemperatureFieldContext context = CreateContext(GetFieldCenter());
            foreach (ITemperatureFieldAffectable affectable in affectedObjects)
            {
                affectable.OnExitTemperatureField(context);
            }

            affectedObjects.Clear();
            detectedThisTick.Clear();
            detectedColliders.Clear();
            detectedCollidersThisTick.Clear();
        }

        private Vector3 GetFieldCenter()
        {
            return transform.TransformPoint(centerOffset);
        }

        private void CreateVisualIfNeeded()
        {
            if (visualRoot != null)
            {
                return;
            }

            GameObject visualRootObject = new GameObject("Temperature Field Visual Root");
            visualRoot = visualRootObject.transform;

            if (showRuntimeSphere)
            {
                visualSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visualSphere.name = "Temperature Field Volume";
                visualSphere.transform.SetParent(visualRoot, false);

                Collider sphereCollider = visualSphere.GetComponent<Collider>();
                if (sphereCollider != null)
                {
                    Destroy(sphereCollider);
                }

                visualRenderer = visualSphere.GetComponent<Renderer>();
                visualMaterial = CreateVisualMaterial();
                visualRenderer.sharedMaterial = visualMaterial;
            }

            CreateRingVisualsIfNeeded();
        }

        private Material CreateVisualMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("HDRP/Unlit") ??
                Shader.Find("Unlit/Transparent") ??
                Shader.Find("Standard");

            Material material = new Material(shader)
            {
                name = "Runtime Temperature Field Material"
            };

            ConfigureMaterialTransparency(material);
            return material;
        }

        private void ConfigureMaterialTransparency(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_SurfaceType"))
            {
                material.SetFloat("_SurfaceType", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
        }

        private void CreateRingVisualsIfNeeded()
        {
            if (!showBoundaryRings && !showGroundProjection)
            {
                return;
            }

            ringMaterial = CreateRingMaterial();

            if (showBoundaryRings)
            {
                horizontalRing = CreateRingRenderer("Temperature Field Horizontal Boundary");
                verticalForwardRing = CreateRingRenderer("Temperature Field Forward Boundary");
                verticalSideRing = CreateRingRenderer("Temperature Field Side Boundary");
            }

            if (showGroundProjection)
            {
                groundProjectionRing = CreateRingRenderer("Temperature Field Ground Projection");
            }
        }

        private LineRenderer CreateRingRenderer(string objectName)
        {
            GameObject ringObject = new GameObject(objectName);
            ringObject.transform.SetParent(visualRoot, false);

            LineRenderer lineRenderer = ringObject.AddComponent<LineRenderer>();
            lineRenderer.sharedMaterial = ringMaterial;
            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = true;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.positionCount = ringSegments;
            return lineRenderer;
        }

        private Material CreateRingMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("HDRP/Unlit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Standard");

            Material material = new Material(shader)
            {
                name = "Runtime Temperature Field Ring Material"
            };

            ConfigureMaterialTransparency(material);
            return material;
        }

        private void UpdateVisual(float alpha, float lineAlphaMultiplier)
        {
            if (visualRoot == null)
            {
                return;
            }

            Vector3 center = GetFieldCenter();
            visualRoot.position = center;
            visualRoot.rotation = Quaternion.identity;

            if (visualSphere != null && visualRenderer != null)
            {
                visualSphere.transform.position = center;
                visualSphere.transform.rotation = Quaternion.identity;
                visualSphere.transform.localScale = Vector3.one * currentRadius * 2f;
                UpdateVolumeMaterial(alpha);
            }

            Color activeBoundaryColor = isCurrentFieldEnhanced ? enhancedBoundaryColor : boundaryColor;
            Color activeGroundColor = isCurrentFieldEnhanced ? enhancedGroundProjectionColor : groundProjectionColor;
            float lineWidthMultiplier = isCurrentFieldEnhanced ? enhancedLineWidthMultiplier : 1f;

            UpdateRing(horizontalRing, center, Vector3.up, activeBoundaryColor, boundaryLineWidth * lineWidthMultiplier, lineAlphaMultiplier);
            UpdateRing(verticalForwardRing, center, Vector3.forward, activeBoundaryColor, boundaryLineWidth * lineWidthMultiplier, lineAlphaMultiplier);
            UpdateRing(verticalSideRing, center, Vector3.right, activeBoundaryColor, boundaryLineWidth * lineWidthMultiplier, lineAlphaMultiplier);

            Vector3 groundCenter = transform.position + Vector3.up * groundProjectionYOffset;
            UpdateRing(groundProjectionRing, groundCenter, Vector3.up, activeGroundColor, groundLineWidth * lineWidthMultiplier, lineAlphaMultiplier);
        }

        private void UpdateVolumeMaterial(float alpha)
        {
            Color color = isCurrentFieldEnhanced ? enhancedFieldColor : fieldColor;
            color.a = Mathf.Clamp01(alpha);

            Material material = visualRenderer.sharedMaterial;
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private void UpdateRing(LineRenderer ring, Vector3 center, Vector3 normal, Color color, float lineWidth, float alphaMultiplier)
        {
            if (ring == null)
            {
                return;
            }

            ring.positionCount = ringSegments;
            ring.startWidth = lineWidth;
            ring.endWidth = lineWidth;

            Color lineColor = color;
            lineColor.a = Mathf.Clamp01(color.a * alphaMultiplier);
            ring.startColor = lineColor;
            ring.endColor = lineColor;

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal.normalized);
            float angleStep = Mathf.PI * 2f / ringSegments;

            for (int i = 0; i < ringSegments; i++)
            {
                float angle = angleStep * i;
                Vector3 localPoint = new Vector3(Mathf.Cos(angle) * currentRadius, 0f, Mathf.Sin(angle) * currentRadius);
                ring.SetPosition(i, center + rotation * localPoint);
            }
        }

        private void DestroyVisual()
        {
            if (visualRoot != null)
            {
                Destroy(visualRoot.gameObject);
                visualRoot = null;
            }

            if (visualMaterial != null)
            {
                Destroy(visualMaterial);
                visualMaterial = null;
            }

            if (ringMaterial != null)
            {
                Destroy(ringMaterial);
                ringMaterial = null;
            }

            visualSphere = null;
            visualRenderer = null;
            horizontalRing = null;
            verticalForwardRing = null;
            verticalSideRing = null;
            groundProjectionRing = null;
        }

        private void AllocateOverlapBuffer()
        {
            if (overlapBuffer == null || overlapBuffer.Length != maxDetectedColliders)
            {
                overlapBuffer = new Collider[maxDetectedColliders];
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showSceneGizmo)
            {
                return;
            }

            bool enhancedPreview = Application.isPlaying ? isCurrentFieldEnhanced || hasStoredEnhancement : hasStoredEnhancement;
            float previewMaxRadius = enhancedPreview ? maxRadius * enhancedRadiusMultiplier : maxRadius;
            float gizmoRadius = Application.isPlaying && currentRadius > 0f ? currentRadius : previewMaxRadius;
            Color gizmoColor = enhancedPreview ? enhancedFieldColor : fieldColor;
            gizmoColor.a = 0.18f;
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(GetFieldCenter(), gizmoRadius);
        }

        private void OnGUI()
        {
            if (showHighTemperatureScreenFrame &&
                HighTemperatureZone.TryGetZoneAtPosition(transform.position, out _))
            {
                DrawScreenFrame(highTemperatureFrameColor, highTemperatureFrameThickness);
            }

            if (showColdTemperatureScreenFrame &&
                ColdTemperatureZone.TryGetZoneAtPosition(transform.position, out _))
            {
                DrawScreenFrame(coldTemperatureFrameColor, coldTemperatureFrameThickness);
            }
        }

        private void DrawScreenFrame(Color frameColor, float frameThickness)
        {
            Color previousColor = GUI.color;
            GUI.color = frameColor;

            float thickness = frameThickness;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - thickness, Screen.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, 0f, thickness, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width - thickness, 0f, thickness, Screen.height), Texture2D.whiteTexture);

            GUI.color = previousColor;
        }
    }
}
