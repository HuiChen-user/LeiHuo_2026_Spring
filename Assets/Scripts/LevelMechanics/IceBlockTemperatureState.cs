using UnityEngine;
using LeiHuo.Gameplay.TemperatureField;

namespace LeiHuo.Gameplay.LevelMechanics
{
    public class IceBlockTemperatureState : MonoBehaviour, ITemperatureFieldAffectable
    {
        public enum MeltMode
        {
            ShrinkThenDestroy,
            DestroyInstantly
        }

        [Header("Melt Timing")]
        [SerializeField, Min(0f)] private float meltDelayAfterLeavingField = 1.5f;
        [SerializeField] private MeltMode meltMode = MeltMode.ShrinkThenDestroy;
        [SerializeField, Min(0.01f)] private float shrinkSpeed = 1f;
        [SerializeField, Min(0f)] private float minimumScaleBeforeDestroy = 0.05f;

        [Header("Behaviour")]
        [SerializeField] private bool resetScaleWhenRefrozen = true;
        [SerializeField] private bool destroyWhenMelted = true;
        [SerializeField] private bool logStateChanges;

        private Vector3 originalScale;
        private float timeWithoutField;
        private bool isInsideTemperatureField;
        private bool isMelting;
        private bool hasBeenInsideTemperatureField;
        private System.Action<IceBlockTemperatureState> meltedCallback;

        public bool IsInsideTemperatureField => isInsideTemperatureField;
        public bool IsMelting => isMelting;
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
            if (!isMelting && timeWithoutField >= meltDelayAfterLeavingField)
            {
                BeginMelting();
            }

            if (isMelting && meltMode == MeltMode.ShrinkThenDestroy)
            {
                TickShrinkMelt();
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

        public void MarkInsideTemperatureField()
        {
            hasBeenInsideTemperatureField = true;
            isInsideTemperatureField = true;
            timeWithoutField = 0f;
            isMelting = false;

            if (resetScaleWhenRefrozen)
            {
                transform.localScale = originalScale;
            }
        }

        public void MarkOutsideTemperatureField()
        {
            isInsideTemperatureField = false;
            timeWithoutField = 0f;
        }

        public void OnEnterTemperatureField(TemperatureFieldContext context)
        {
            MarkInsideTemperatureField();
        }

        public void OnStayTemperatureField(TemperatureFieldContext context)
        {
            MarkInsideTemperatureField();
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

            if (meltMode == MeltMode.DestroyInstantly)
            {
                FinishMelt();
            }
        }

        private void TickShrinkMelt()
        {
            float step = shrinkSpeed * Time.deltaTime;
            transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.zero, step);

            if (transform.localScale.magnitude <= minimumScaleBeforeDestroy)
            {
                FinishMelt();
            }
        }

        private void FinishMelt()
        {
            meltedCallback?.Invoke(this);

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

        private void OnValidate()
        {
            meltDelayAfterLeavingField = Mathf.Max(0f, meltDelayAfterLeavingField);
            shrinkSpeed = Mathf.Max(0.01f, shrinkSpeed);
            minimumScaleBeforeDestroy = Mathf.Max(0f, minimumScaleBeforeDestroy);
        }
    }
}
