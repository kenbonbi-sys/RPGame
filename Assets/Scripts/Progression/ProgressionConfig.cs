using UnityEngine;

namespace RPG
{
    public enum EnemyRank
    {
        Normal,
        Elite,
        MiniBoss,
        Boss
    }

    public enum CoreStat
    {
        Strength,
        Intelligence,
        Agility,
        Vitality
    }

    /// <summary>
    /// Every number of the level / attribute / XP rules (plan §05), editable in the Inspector
    /// at Assets/Data/Progression.asset. The formula methods are pure so they can be tested.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Progression Config")]
    public class ProgressionConfig : ScriptableObject
    {
        [Header("Levels")]
        public int maxLevel = 40;
        [Tooltip("XP to reach the next level = xpBase × level ^ xpExponent")]
        public float xpBase = 50f;
        public float xpExponent = 1.6f;
        public int statPointsPerLevel = 3;
        public int talentPointsPerLevel = 1;
        public int skillPointsPerLevel = 1;

        [Header("Starting attributes (level 1)")]
        public int startStrength = 3;
        public int startIntelligence = 3;
        public int startAgility = 3;
        public int startVitality = 4;

        [Header("Health & energy")]
        public float hpBase = 55f;
        public float hpPerLevel = 9f;
        public float hpPerVitality = 10f;
        public float energyBase = 45f;
        public float energyPerLevel = 1.5f;
        public float energyPerIntelligence = 3f;

        [Header("Attack")]
        [Tooltip("Weapon attack until equipment exists (Kiếm Sắt).")]
        public float weaponAttack = 20f;
        public float attackPerStrength = 1.5f;
        public float attackPerIntelligence = 1.5f;

        [Header("Crit")]
        [Tooltip("Crit chance added per Agility point, on top of each skill's own crit chance.")]
        public float critPerAgility = 0.0025f;
        public float critMax = 0.6f;
        public float critMultiplier = 1.8f;

        [Header("Defence")]
        public float armorPerVitality = 1f;
        public float resistPerVitality = 0.003f;
        public float resistMax = 0.75f;
        [Tooltip("Lowest resistance: a weakness makes a type deal up to this much more (−0.5 = +50%).")]
        public float resistMin = -0.5f;
        [Tooltip("Reduction = armor / (armor + armorConstant + armorPerAttackerLevel × attacker level)")]
        public float armorConstant = 50f;
        public float armorPerAttackerLevel = 5f;

        [Header("Damage")]
        [Tooltip("Final damage × a random factor in 1 ± spread (plan §04: 0.95–1.05). 0 = no randomness.")]
        public float damageSpread = 0.05f;

        [Header("Other attribute effects")]
        public float attackSpeedPerAgility = 0.005f;
        public float dashCooldownPerAgility = 0.005f;
        public float poisePerStrength = 0.01f;

        [Header("Kill XP = (base + perLevel × enemy level) × rank multiplier")]
        public float killXpBase = 8f;
        public float killXpPerLevel = 4f;
        [Tooltip("Normal, Elite, Mini-boss, Boss")]
        public float[] rankMultiplier = { 1f, 4f, 15f, 40f };
        [Tooltip("Enemy this many levels below the player gives the reduced share.")]
        public int lowGap = 5;
        public float lowGapFactor = 0.5f;
        public int veryLowGap = 8;
        public float veryLowGapFactor = 0.1f;

        // ------------------------------------------------------------------ formulas
        public int XpToNext(int level)
        {
            if (level >= maxLevel) return 0;
            return Mathf.RoundToInt(xpBase * Mathf.Pow(Mathf.Max(1, level), xpExponent));
        }

        /// <summary>Total XP needed to go from level 1 to <paramref name="level"/>.</summary>
        public int TotalXpTo(int level)
        {
            int sum = 0;
            for (int l = 1; l < Mathf.Min(level, maxLevel); l++) sum += XpToNext(l);
            return sum;
        }

        public int KillXp(int enemyLevel, EnemyRank rank, int playerLevel)
        {
            int r = Mathf.Clamp((int)rank, 0, rankMultiplier.Length - 1);
            float xp = (killXpBase + killXpPerLevel * enemyLevel) * rankMultiplier[r];
            int gap = playerLevel - enemyLevel;
            if (gap >= veryLowGap) xp *= veryLowGapFactor;
            else if (gap >= lowGap) xp *= lowGapFactor;
            return Mathf.Max(1, Mathf.RoundToInt(xp));
        }

        public int StartValue(CoreStat a)
        {
            switch (a)
            {
                case CoreStat.Strength: return startStrength;
                case CoreStat.Intelligence: return startIntelligence;
                case CoreStat.Agility: return startAgility;
                default: return startVitality;
            }
        }

        public float MaxHp(int level, float vitality) => hpBase + hpPerLevel * level + hpPerVitality * vitality;

        public float MaxEnergy(int level, float intelligence) => energyBase + energyPerLevel * level + energyPerIntelligence * intelligence;

        public float PhysicalAttack(float strength) => weaponAttack + attackPerStrength * strength;

        public float MagicAttack(float intelligence) => weaponAttack + attackPerIntelligence * intelligence;

        /// <summary>
        /// Until the Ability System v2 multiplies skill power by Attack, existing skill numbers are
        /// scaled by Attack relative to a fresh level-1 hero, so starting stats keep the prototype balance.
        /// </summary>
        public float DamageScale(float attack, bool magic)
        {
            float reference = magic ? MagicAttack(startIntelligence) : PhysicalAttack(startStrength);
            return reference > 0 ? attack / reference : 1f;
        }

        public float ArmorReduction(float armor, int attackerLevel)
        {
            if (armor <= 0) return 0f;
            return armor / (armor + armorConstant + armorPerAttackerLevel * Mathf.Max(1, attackerLevel));
        }

        /// <summary>Damage multiplier for a resistance: 0.75 → ×0.25, a −0.3 weakness → ×1.3.</summary>
        public float ResistMultiplier(float resist) => 1f - Mathf.Clamp(resist, resistMin, resistMax);

        /// <summary>The random spread for a roll in 0..1: 0 → 1 − spread, 0.5 → 1, 1 → 1 + spread.</summary>
        public float Spread(float roll01) => 1f + (Mathf.Clamp01(roll01) * 2f - 1f) * damageSpread;

        /// <summary>
        /// The defender's side of plan §04: armor, then resistance, then the random spread.
        /// The attacker's side (Attack × skill power × bonuses × crit) is already in <paramref name="amount"/>.
        /// Example of the plan: Cầu Lửa 140% of Attack 40 = 56, on a −30% fire weakness with 10 armor
        /// against a level 5 attacker, rolled 0.5: 56 × 0.882 × 1.3 × 1 ≈ 64.
        /// </summary>
        public float Mitigate(float amount, float armor, int attackerLevel, float resist, float roll01) =>
            amount * (1f - ArmorReduction(armor, attackerLevel)) * ResistMultiplier(resist) * Spread(roll01);

        static ProgressionConfig fallback;

        /// <summary>The config in use: the database's, or built-in defaults when none is assigned.</summary>
        public static ProgressionConfig Current
        {
            get
            {
                var gm = GameManager.I;
                if (gm != null && gm.db != null && gm.db.progression != null) return gm.db.progression;
                if (fallback == null)
                {
                    fallback = CreateInstance<ProgressionConfig>();
                    fallback.hideFlags = HideFlags.DontSave;
                }
                return fallback;
            }
        }
    }
}
