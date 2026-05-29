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
        public bool IsCasterInHighTemperatureZone;
        public HighTemperatureZone HighTemperatureZone;
        public bool HotZoneRequiresEnhancementToFreezeVapor;
        public float HotZoneUncontrolledSlowSpeedMultiplier;
        public float HotZoneUncontrolledSlowDuration;
        public float HotZoneEnhancedStopDuration;

        public TemperatureFieldContext(
            GameObject caster,
            Transform casterTransform,
            Vector3 center,
            float radius,
            float normalizedRadius,
            float elapsedTime,
            bool isEnhanced = false,
            float strengthMultiplier = 1f,
            HighTemperatureZone highTemperatureZone = null)
        {
            Caster = caster;
            CasterTransform = casterTransform;
            Center = center;
            Radius = radius;
            NormalizedRadius = normalizedRadius;
            ElapsedTime = elapsedTime;
            IsEnhanced = isEnhanced;
            StrengthMultiplier = Mathf.Max(1f, strengthMultiplier);
            IsCasterInHighTemperatureZone = highTemperatureZone != null;
            HighTemperatureZone = highTemperatureZone;
            HotZoneRequiresEnhancementToFreezeVapor = highTemperatureZone == null || highTemperatureZone.RequireEnhancedFieldToFreezeVapor;
            HotZoneUncontrolledSlowSpeedMultiplier = highTemperatureZone != null ? highTemperatureZone.UncontrolledSlowSpeedMultiplier : 1f;
            HotZoneUncontrolledSlowDuration = highTemperatureZone != null ? highTemperatureZone.UncontrolledSlowDurationAfterLeavingField : 0f;
            HotZoneEnhancedStopDuration = highTemperatureZone != null ? highTemperatureZone.EnhancedStopDurationAfterLeavingField : 0f;
        }
    }
}
