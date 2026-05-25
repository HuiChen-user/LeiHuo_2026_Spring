using UnityEngine;

namespace LeiHuo.Gameplay.LevelMechanics
{
    public readonly struct AttackContext
    {
        public AttackContext(
            GameObject attacker,
            Transform origin,
            Vector3 attackCenter,
            Vector3 attackDirection,
            GameObject target,
            Collider targetCollider,
            float distance,
            int attackId,
            float time)
        {
            Attacker = attacker;
            Origin = origin;
            AttackCenter = attackCenter;
            AttackDirection = attackDirection;
            Target = target;
            TargetCollider = targetCollider;
            Distance = distance;
            AttackId = attackId;
            Time = time;
        }

        public GameObject Attacker { get; }
        public Transform Origin { get; }
        public Vector3 AttackCenter { get; }
        public Vector3 AttackDirection { get; }
        public GameObject Target { get; }
        public Collider TargetCollider { get; }
        public float Distance { get; }
        public int AttackId { get; }
        public float Time { get; }
    }
}
