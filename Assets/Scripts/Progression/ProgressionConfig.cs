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

    /// <summary>
    /// The six ability scores of D&D 5e. The first four keep the numbers of the old attributes
    /// (saves and requests store them as numbers): Agility became Khéo Léo, Vitality Thể Chất.
    /// </summary>
    public enum CoreStat
    {
        /// <summary>Sức Mạnh (STR): heavy weapons, Trấn Áp.</summary>
        Strength,
        /// <summary>Trí Tuệ (INT): a Pháp Sư's spells, shorter cooldowns.</summary>
        Intelligence,
        /// <summary>Khéo Léo (DEX): finesse and ranged weapons, crits, attack speed, Lướt.</summary>
        Dexterity,
        /// <summary>Thể Chất (CON): health and armor.</summary>
        Constitution,
        /// <summary>Thông Thái (WIS): divine and nature spells, healing, resistance.</summary>
        Wisdom,
        /// <summary>Sức Hút (CHA): the spells of bards, sorcerers, warlocks and paladins, gold.</summary>
        Charisma
    }

    /// <summary>Names, order and D&D arithmetic of the six ability scores.</summary>
    public static class CoreStats
    {
        public const int Count = 6;

        /// <summary>The order of a D&D character sheet: STR DEX CON INT WIS CHA.</summary>
        public static readonly CoreStat[] SheetOrder =
        {
            CoreStat.Strength, CoreStat.Dexterity, CoreStat.Constitution, CoreStat.Intelligence, CoreStat.Wisdom, CoreStat.Charisma
        };

        /// <summary>D&D's standard array: every hero's six starting scores, placed by their class.</summary>
        public static readonly int[] StandardArray = { 15, 14, 13, 12, 10, 8 };

        /// <summary>
        /// The scores of a hero with no class yet (an older character before the creator, by CoreStat):
        /// they give the prototype's hero back — 104 health, its damage and energy.
        /// </summary>
        public static readonly int[] Unclassed = { 16, 16, 11, 14, 10, 10 };

        public static StatId Stat(CoreStat a)
        {
            switch (a)
            {
                case CoreStat.Strength: return StatId.Strength;
                case CoreStat.Intelligence: return StatId.Intelligence;
                case CoreStat.Dexterity: return StatId.Dexterity;
                case CoreStat.Constitution: return StatId.Constitution;
                case CoreStat.Wisdom: return StatId.Wisdom;
                default: return StatId.Charisma;
            }
        }

        public static string Name(CoreStat a)
        {
            switch (a)
            {
                case CoreStat.Strength: return "Sức Mạnh";
                case CoreStat.Intelligence: return "Trí Tuệ";
                case CoreStat.Dexterity: return "Khéo Léo";
                case CoreStat.Constitution: return "Thể Chất";
                case CoreStat.Wisdom: return "Thông Thái";
                default: return "Sức Hút";
            }
        }

        /// <summary>The D&D abbreviation (STR, DEX…), as players of the tabletop game know them.</summary>
        public static string Short(CoreStat a)
        {
            switch (a)
            {
                case CoreStat.Strength: return "STR";
                case CoreStat.Intelligence: return "INT";
                case CoreStat.Dexterity: return "DEX";
                case CoreStat.Constitution: return "CON";
                case CoreStat.Wisdom: return "WIS";
                default: return "CHA";
            }
        }

        /// <summary>D&D's ability modifier: 10–11 → +0, 16–17 → +3, 8–9 → −1.</summary>
        public static int Modifier(int score) => Mathf.FloorToInt((score - 10) / 2f);

        public static string ModifierText(int score)
        {
            int m = Modifier(score);
            return m >= 0 ? "+" + m : m.ToString();
        }
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

        [Header("Ability scores (D&D: 10 is the average person)")]
        [Tooltip("The score a fresh level-1 hero has in the ability they fight with: the prototype's balance is kept for it.")]
        public int referenceScore = 16;

        [Header("Health & energy")]
        public float hpBase = 56f;
        [Tooltip("Health per level for each point of the class's average hit die (d8 → 4.5 × this).")]
        public float hpPerHitDie = 1.8f;
        [Tooltip("Health per point of Thể Chất above 10.")]
        public float hpPerConstitution = 10f;
        public float energyBase = 45f;
        public float energyPerLevel = 1.5f;
        [Tooltip("Energy per point above 10 of the ability the class casts with.")]
        public float energyPerCastingPoint = 1.5f;

        [Header("Attack")]
        [Tooltip("Weapon attack until equipment exists (Kiếm Sắt).")]
        public float weaponAttack = 20f;
        [Tooltip("Attack per point of the ability a hero fights (or casts) with, counted from attackFromScore.")]
        public float attackPerPoint = 1.5f;
        [Tooltip("The score whose attack is the weapon's alone: 16 in the fighting ability (+3) gives the prototype's 24.5 with a 20 weapon.")]
        public float attackFromScore = 13f;

        [Header("Crit")]
        [Tooltip("Crit chance added per point of Khéo Léo above 10, on top of each skill's own crit chance.")]
        public float critPerDexterity = 0.0025f;
        public float critMax = 0.6f;
        public float critMultiplier = 1.8f;

        [Header("Defence")]
        [Tooltip("Armor per point of Thể Chất above 10, on top of the class's armor.")]
        public float armorPerConstitution = 0.5f;
        [Tooltip("Elemental resistance per point of Thông Thái above 10.")]
        public float resistPerWisdom = 0.003f;
        public float resistMax = 0.75f;
        [Tooltip("Lowest resistance: a weakness makes a type deal up to this much more (−0.5 = +50%).")]
        public float resistMin = -0.5f;
        [Tooltip("Reduction = armor / (armor + armorConstant + armorPerAttackerLevel × attacker level)")]
        public float armorConstant = 50f;
        public float armorPerAttackerLevel = 5f;

        [Header("Damage")]
        [Tooltip("Final damage × a random factor in 1 ± spread (plan §04: 0.95–1.05). 0 = no randomness.")]
        public float damageSpread = 0.05f;

        [Header("Other ability effects (per point above 10)")]
        public float attackSpeedPerDexterity = 0.005f;
        public float dashCooldownPerDexterity = 0.005f;
        public float poisePerStrength = 0.01f;
        [Tooltip("Cooldown taken off every skill but the basic attack, per point of Trí Tuệ above 10.")]
        public float cooldownPerIntelligence = 0.004f;
        public float cooldownReductionMax = 0.3f;
        [Tooltip("Healing done, per point of Thông Thái above 10.")]
        public float healingPerWisdom = 0.02f;
        [Tooltip("Gold found, per point of Sức Hút above 10.")]
        public float goldPerCharisma = 0.02f;

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

        /// <summary>Health: a base, the class's hit die for every level (D&D: d6 to d12), and Thể Chất.</summary>
        public float MaxHp(int level, float hitDieAverage, float constitution) =>
            hpBase + hpPerHitDie * hitDieAverage * level + hpPerConstitution * (constitution - 10f);

        /// <summary>Energy: a base, the level, and the ability the class casts with.</summary>
        public float MaxEnergy(int level, float castingScore) =>
            energyBase + energyPerLevel * level + energyPerCastingPoint * (castingScore - 10f);

        /// <summary>Attack of a weapon (or a spell) wielded with an ability score: D&D adds the ability to the blow.</summary>
        public float Attack(float score, float weapon) => weapon + attackPerPoint * (score - attackFromScore);

        /// <summary>Attack with the plain starting weapon.</summary>
        public float PhysicalAttack(float score) => Attack(score, weaponAttack);

        public float MagicAttack(float score) => Attack(score, weaponAttack);

        /// <summary>
        /// Until the Ability System v2 multiplies skill power by Attack, existing skill numbers are
        /// scaled by Attack relative to a fresh level-1 hero with <see cref="referenceScore"/> in
        /// the ability they fight with, so such a hero keeps the prototype balance.
        /// </summary>
        public float DamageScale(float attack, bool magic)
        {
            float reference = Attack(referenceScore, weaponAttack);
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
