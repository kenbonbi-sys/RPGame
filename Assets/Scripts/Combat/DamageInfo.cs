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
        public StatusHit status;   // Bỏng, Lạnh, Tích Điện, Độc, Choáng, Trói, Làm Chậm, Nguyền, Phán Xét
        public float hitStop;
        public float poise;        // Thanh Trấn Áp damage (bosses, elites)
        public string skillName;   // shown in logs
        public int sourceLevel;    // attacker level for armor; 0 = read it from the source's Health
        public bool attackScaled;  // amount already includes the attacker's Attack (abilities)
        public bool pure;          // skips armor, resistances, damage-taken multipliers and the random spread (Kill, scripts)
        public bool contact;       // bumping into an enemy's body, not an attack: Lướt Hoàn Hảo ignores it
        public bool dot;           // a damage-over-time tick (Bỏng, Độc), not an attack either
        public bool feedback;      // its knockback, flash and sparks show when it lands (Combat.OnHitFeedback)
        public bool held;          // online, a hit the server held back for a moment (LagCompensation) that lands now

        /// <summary>The hero behind the hit (its caster, or the owner of its projectile), or null.</summary>
        public PlayerController SourcePlayer => source != null ? source.GetComponentInParent<PlayerController>() : null;

        /// <summary>The stats of the hero behind the hit (Attack, crit and Trấn Áp bonuses), or null.</summary>
        public PlayerStats SourceStats => source != null ? source.GetComponentInParent<PlayerStats>() : null;

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
        /// Rolls a crit. A hero adds their Agility bonus to the skill's chance and uses the
        /// crit multiplier from their stats; everyone else crits for ×1.8.
        /// </summary>
        public DamageInfo RollCrit(float chance)
        {
            float mult = 1.8f;
            var stats = sourceTeam == Team.Player ? SourceStats : null;
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
