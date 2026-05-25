using UnityEngine;

namespace LeiHuo.Gameplay.TemperatureField
{
    public struct TemperatureFieldContext
    {
        public GameObject Caster;
        public Transform CasterTransform;
        public Vector3 Center;
        public float Radius;
        public float NormalizedRadius;
        public float ElapsedTime;
        public bool IsEnhanced;
        public float StrengthMultiplier;

        public TemperatureFieldContext(
            GameObject caster,
            Transform casterTransform,
            Vector3 center,
            float radius,
            float normalizedRadius,
            float elapsedTime,
            bool isEnhanced = false,
            float strengthMultiplier = 1f)
        {
            Caster = caster;
            CasterTransform = casterTransform;
            Center = center;
            Radius = radius;
            NormalizedRadius = normalizedRadius;
            ElapsedTime = elapsedTime;
            IsEnhanced = isEnhanced;
            StrengthMultiplier = Mathf.Max(1f, strengthMultiplier);
        }
    }
}
