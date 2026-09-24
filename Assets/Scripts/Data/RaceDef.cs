using UnityEngine;

namespace RPG
{
    /// <summary>How tall a people stands (the sprite builder shortens legs and body).</summary>
    public enum BodyBuild
    {
        Normal,
        /// <summary>Người Lùn: a head shorter, broad.</summary>
        Stout,
        /// <summary>Người Tí Hon, Thần Lùn: small.</summary>
        Small
    }

    /// <summary>
    /// A people a hero can be born to (D&D 5e, Player's Handbook 2014): its ability score
    /// increases, its traits (a few turned into this game's rules: darkvision lights the dark,
    /// resilience shortens poison, luck turns a blow aside…) and the looks the sprite builder
    /// draws (ears, horns, tusks, a tail, a dragon's head, skin colours). Made by AssetFactory.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Race")]
    public class RaceDef : ScriptableObject
    {
        public string id;
        public string displayName;
        [Tooltip("Its name in D&D (Human, Elf…).")]
        public string englishName;
        [TextArea(2, 4)] public string description;

        [Header("Ability score increases (STR INT DEX CON WIS CHA, as CoreStat)")]
        public int[] bonus = new int[CoreStats.Count];

        [Header("Traits")]
        [Tooltip("One line per trait: \"Tên: what it does\".")]
        [TextArea(2, 6)] public string traits;
        [Tooltip("D&D walking speed 25 ft against 30: a little slower.")]
        public float speedMultiplier = 1f;
        [Tooltip("Nhìn Trong Tối: the hero's own light reaches further in the dark.")]
        public bool darkvision;
        [Tooltip("Resistance to damage types (Người Lùn: poison; Quỷ Duệ: fire; Long Duệ: its ancestry's).")]
        public Resistances resist;
        [Tooltip("Share taken off Độc on this hero (Người Lùn: Dwarven Resilience).")]
        [Range(0, 1)] public float poisonShorter;
        [Tooltip("Share taken off Choáng and Nguyền on this hero (Tiên: Fey Ancestry; Tí Hon: Brave).")]
        [Range(0, 1)] public float controlShorter;
        [Tooltip("Chance a blow misses this hero outright (Người Tí Hon: Lucky).")]
        [Range(0, 1)] public float luckyDodge;
        [Tooltip("Seconds between two saves from a lethal blow at 1 health (Bán Orc: Relentless Endurance; 0: never).")]
        public float relentlessCooldown;
        [Tooltip("Extra crit damage (Bán Orc: Savage Attacks).")]
        public float savageCrit;
        [Tooltip("Chance to set an attacker alight when hit (Quỷ Duệ: Hellish Rebuke).")]
        [Range(0, 1)] public float rebukeChance;
        [Tooltip("Extra share of experience (Con Người: versatile).")]
        public float xpBonus;
        [Tooltip("Extra health for every level (Người Lùn: Dwarven Toughness).")]
        public float hpPerLevel;
        [Tooltip("Long Duệ: its scales' colour chooses its ancestry's element.")]
        public bool draconic;

        [Header("Looks (the sprite builder)")]
        public BodyBuild build;
        [Tooltip("Skin (or scale) colours to choose from, lightest first.")]
        public Color[] skins = { new Color(1f, 0.84f, 0.71f) };
        public bool pointedEars;
        public bool horns;
        public bool tail;
        public bool tusks;
        [Tooltip("A dragon's head instead of a face and hair.")]
        public bool dragonHead;
        [Tooltip("Hair styles its people wear (indices of HeroLook.HairStyles); empty: all.")]
        public int[] hairStyles;
        [Tooltip("Beards are common (Người Lùn): the creator offers them first.")]
        public bool beards;

        public int Bonus(CoreStat a) => bonus != null && (int)a < bonus.Length ? bonus[(int)a] : 0;

        /// <summary>"+2 Khéo Léo, +1 Trí Tuệ" (the creator's cards).</summary>
        public string BonusText()
        {
            bool allOne = true;
            foreach (var a in CoreStats.SheetOrder) allOne &= Bonus(a) == 1;
            if (allOne) return "+1 mọi chỉ số";
            var sb = new System.Text.StringBuilder();
            foreach (var a in CoreStats.SheetOrder)
            {
                int b = Bonus(a);
                if (b == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(b > 0 ? "+" : "").Append(b).Append(' ').Append(CoreStats.Name(a));
            }
            return sb.ToString();
        }
    }
}
