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

        public TemperatureFieldContext(
            GameObject caster,
            Transform casterTransform,
            Vector3 center,
            float radius,
            float normalizedRadius,
            float elapsedTime)
        {
            Caster = caster;
            CasterTransform = casterTransform;
            Center = center;
            Radius = radius;
            NormalizedRadius = normalizedRadius;
            ElapsedTime = elapsedTime;
        }
    }
}
