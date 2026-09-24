using UnityEngine;

namespace RPG
{
    public struct DamageInfo
    {
        public float amount;
        public DamageType type;
        public Team sourceTeam;
        public GameObject source;
        public Vector2 point;
        public Vector2 direction;
        public float knockback;
        public bool crit;
        public float stun;
        public float slow;        // 0..1 speed reduction
        public float slowDuration;
        public float burnDps;
        public float burnDuration;
        public float hitStop;
        public string skillName;   // shown in logs

        public static DamageInfo Make(float amount, Team team, GameObject source, Vector2 point, Vector2 dir,
                                      DamageType type = DamageType.Physical, float knockback = 0f)
        {
            return new DamageInfo
            {
                amount = amount,
                sourceTeam = team,
                source = source,
                point = point,
                direction = dir.sqrMagnitude > 0 ? dir.normalized : Vector2.down,
                type = type,
                knockback = knockback
            };
        }

        /// <summary>Rolls a crit (x1.8) with the given chance.</summary>
        public DamageInfo RollCrit(float chance)
        {
            if (Random.value < chance)
            {
                crit = true;
                amount *= 1.8f;
            }
            return this;
        }
    }
}
