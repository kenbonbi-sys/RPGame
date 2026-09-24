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
        public float poise;        // Thanh Trấn Áp damage (bosses, elites)
        public string skillName;   // shown in logs
        public int sourceLevel;    // attacker level for armor; 0 = read it from the source's Health
        public bool attackScaled;  // amount already includes the attacker's Attack (abilities)
        public bool pure;          // skips armor, resistances, damage-taken multipliers and the random spread (Kill, scripts)

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

        /// <summary>
        /// Rolls a crit. The hero adds the Agility bonus to the skill's chance and uses the
        /// crit multiplier from the stats; everyone else crits for ×1.8.
        /// </summary>
        public DamageInfo RollCrit(float chance)
        {
            float mult = 1.8f;
            var stats = sourceTeam == Team.Player ? PlayerStats.I : null;
            if (stats != null)
            {
                chance = stats.CritChance(chance);
                mult = stats.CritMultiplier;
            }
            if (chance > 0 && Random.value < chance)
            {
                crit = true;
                amount *= mult;
            }
            return this;
        }
    }
}
