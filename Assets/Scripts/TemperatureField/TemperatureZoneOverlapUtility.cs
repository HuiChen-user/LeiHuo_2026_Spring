using UnityEngine;

namespace LeiHuo.Gameplay.TemperatureField
{
    internal static class TemperatureZoneOverlapUtility
    {
        private const float Epsilon = 0.0001f;

        public static bool Overlaps(HighTemperatureZone highZone, ColdTemperatureZone coldZone)
        {
            if (highZone == null || coldZone == null)
            {
                return false;
            }

            if (highZone.CurrentShape == HighTemperatureZone.ZoneShape.Sphere &&
                coldZone.CurrentShape == ColdTemperatureZone.ZoneShape.Sphere)
            {
                return SpheresOverlap(
                    highZone.WorldCenter,
                    highZone.Radius,
                    coldZone.WorldCenter,
                    coldZone.Radius);
            }

            if (highZone.CurrentShape == HighTemperatureZone.ZoneShape.Sphere)
            {
                return SphereBoxOverlap(
                    highZone.WorldCenter,
                    highZone.Radius,
                    coldZone.WorldCenter,
                    coldZone.WorldRotation,
                    coldZone.BoxSize);
            }

            if (coldZone.CurrentShape == ColdTemperatureZone.ZoneShape.Sphere)
            {
                return SphereBoxOverlap(
                    coldZone.WorldCenter,
                    coldZone.Radius,
                    highZone.WorldCenter,
                    highZone.WorldRotation,
                    highZone.BoxSize);
            }

            return BoxesOverlap(
                highZone.WorldCenter,
                highZone.WorldRotation,
                highZone.BoxSize,
                coldZone.WorldCenter,
                coldZone.WorldRotation,
                coldZone.BoxSize);
        }

        private static bool SpheresOverlap(Vector3 centerA, float radiusA, Vector3 centerB, float radiusB)
        {
            float combinedRadius = radiusA + radiusB;
            return (centerA - centerB).sqrMagnitude <= combinedRadius * combinedRadius;
        }

        private static bool SphereBoxOverlap(
            Vector3 sphereCenter,
            float sphereRadius,
            Vector3 boxCenter,
            Quaternion boxRotation,
            Vector3 boxSize)
        {
            Vector3 localSphereCenter = Quaternion.Inverse(boxRotation) * (sphereCenter - boxCenter);
            Vector3 halfSize = boxSize * 0.5f;
            Vector3 closestPoint = new Vector3(
                Mathf.Clamp(localSphereCenter.x, -halfSize.x, halfSize.x),
                Mathf.Clamp(localSphereCenter.y, -halfSize.y, halfSize.y),
                Mathf.Clamp(localSphereCenter.z, -halfSize.z, halfSize.z));

            return (localSphereCenter - closestPoint).sqrMagnitude <= sphereRadius * sphereRadius;
        }

        private static bool BoxesOverlap(
            Vector3 centerA,
            Quaternion rotationA,
            Vector3 sizeA,
            Vector3 centerB,
            Quaternion rotationB,
            Vector3 sizeB)
        {
            Vector3[] axesA =
            {
                rotationA * Vector3.right,
                rotationA * Vector3.up,
                rotationA * Vector3.forward
            };

            Vector3[] axesB =
            {
                rotationB * Vector3.right,
                rotationB * Vector3.up,
                rotationB * Vector3.forward
            };

            Vector3 halfA = sizeA * 0.5f;
            Vector3 halfB = sizeB * 0.5f;
            float[,] rotation = new float[3, 3];
            float[,] absRotation = new float[3, 3];

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    rotation[i, j] = Vector3.Dot(axesA[i], axesB[j]);
                    absRotation[i, j] = Mathf.Abs(rotation[i, j]) + Epsilon;
                }
            }

            Vector3 centerDelta = centerB - centerA;
            Vector3 localDelta = new Vector3(
                Vector3.Dot(centerDelta, axesA[0]),
                Vector3.Dot(centerDelta, axesA[1]),
                Vector3.Dot(centerDelta, axesA[2]));

            float[] extentsA = { halfA.x, halfA.y, halfA.z };
            float[] extentsB = { halfB.x, halfB.y, halfB.z };
            float[] delta = { localDelta.x, localDelta.y, localDelta.z };

            for (int i = 0; i < 3; i++)
            {
                float radiusA = extentsA[i];
                float radiusB = extentsB[0] * absRotation[i, 0] + extentsB[1] * absRotation[i, 1] + extentsB[2] * absRotation[i, 2];
                if (Mathf.Abs(delta[i]) > radiusA + radiusB)
                {
                    return false;
                }
            }

            for (int j = 0; j < 3; j++)
            {
                float radiusA = extentsA[0] * absRotation[0, j] + extentsA[1] * absRotation[1, j] + extentsA[2] * absRotation[2, j];
                float radiusB = extentsB[j];
                float projectedDelta = Mathf.Abs(delta[0] * rotation[0, j] + delta[1] * rotation[1, j] + delta[2] * rotation[2, j]);
                if (projectedDelta > radiusA + radiusB)
                {
                    return false;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    float radiusA = extentsA[(i + 1) % 3] * absRotation[(i + 2) % 3, j] +
                                    extentsA[(i + 2) % 3] * absRotation[(i + 1) % 3, j];
                    float radiusB = extentsB[(j + 1) % 3] * absRotation[i, (j + 2) % 3] +
                                    extentsB[(j + 2) % 3] * absRotation[i, (j + 1) % 3];
                    float projectedDelta = Mathf.Abs(delta[(i + 2) % 3] * rotation[(i + 1) % 3, j] -
                                                     delta[(i + 1) % 3] * rotation[(i + 2) % 3, j]);

                    if (projectedDelta > radiusA + radiusB)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
