using UnityEngine;

namespace RPG
{
    /// <summary>How a class protects itself (D&D armor proficiency): its base armor.</summary>
    public enum ArmorKind
    {
        /// <summary>Robes (Pháp Sư, Thuật Sĩ, Khế Ước Sư).</summary>
        None,
        Light,
        Medium,
        Heavy,
        /// <summary>No armor but a body trained for it (Cuồng Chiến Binh: Thể Chất; Võ Tăng: Thông Thái).</summary>
        Unarmored
    }

    /// <summary>How hard a class is to play (D&D Beyond's filter on its class page).</summary>
    public enum Complexity
    {
        Low,
        Average,
        High
    }

    /// <summary>
    /// A class a hero can take (the twelve of D&D 5e): its hit die, the abilities it lives by
    /// and where the standard array goes, its saving throws, the ability it casts with, its
    /// armor, the weapons it may carry and the skills on its bar. The creator shows it as a card
    /// (colour, emblem, tags) like D&D Beyond's class page. Made by AssetFactory.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Class")]
    public class ClassDef : ScriptableObject
    {
        public string id;
        public string displayName;
        [Tooltip("Its name in D&D (Barbarian, Bard…).")]
        public string englishName;
        [Tooltip("The card's first tag, in capitals (CHIẾN BINH CUỒNG NỘ).")]
        public string role;
        [TextArea(2, 4)] public string description;
        public Complexity complexity = Complexity.Average;

        [Header("Rules (D&D 5e)")]
        [Tooltip("Sides of the hit die: 6, 8, 10 or 12.")]
        public int hitDie = 8;
        [Tooltip("The abilities it lives by (the card's second tag).")]
        public CoreStat[] primary = { CoreStat.Strength };
        [Tooltip("Where the standard array 15 14 13 12 10 8 goes, highest first.")]
        public CoreStat[] arrayOrder =
        {
            CoreStat.Strength, CoreStat.Constitution, CoreStat.Dexterity, CoreStat.Wisdom, CoreStat.Charisma, CoreStat.Intelligence
        };
        public CoreStat[] savingThrows = { CoreStat.Strength, CoreStat.Constitution };
        [Tooltip("The ability its spells (and its energy) come from; martial classes: the one they fight with.")]
        public CoreStat casting = CoreStat.Strength;
        public ArmorKind armor = ArmorKind.Light;

        [Header("Weapons and skills")]
        [Tooltip("Weapon kinds it can carry (WeaponKinds ids), the first one by default.")]
        public string[] weapons = { "sword" };
        [Tooltip("Skills of the bar Q W E R A S D (ability ids). An empty Q: the weapon's own basic attack.")]
        public string[] kit = new string[7];

        [Header("Looks")]
        [Tooltip("The outfit the sprite builder draws (HeroArt outfits).")]
        public string outfit = "fighter";
        [Tooltip("The outfit's default main colour (players may change it).")]
        public Color cloth = new Color(0.55f, 0.2f, 0.2f);
        [Tooltip("The card's colour (D&D Beyond gives each class its own).")]
        public Color color = new Color(0.8f, 0.5f, 0.2f);
        public Sprite emblem;

        public float HitDieAverage => hitDie / 2f + 0.5f;

        /// <summary>Its starting score in an ability: the standard array placed by <see cref="arrayOrder"/>.</summary>
        public int ArrayScore(CoreStat a)
        {
            int i = System.Array.IndexOf(arrayOrder, a);
            return i >= 0 && i < CoreStats.StandardArray.Length ? CoreStats.StandardArray[i] : 10;
        }

        public bool Saves(CoreStat a) => System.Array.IndexOf(savingThrows, a) >= 0;

        public bool Allows(string weapon) => System.Array.IndexOf(weapons, weapon) >= 0;

        public string DefaultWeapon => weapons != null && weapons.Length > 0 ? weapons[0] : "sword";

        /// <summary>"SỨC MẠNH" or "SỨC MẠNH · SỨC HÚT" (the card's second tag).</summary>
        public string PrimaryText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var a in primary)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(CoreStats.Name(a).ToUpperInvariant());
            }
            return sb.ToString();
        }

        public static string ComplexityName(Complexity c) => c == Complexity.Low ? "Dễ" : c == Complexity.Average ? "Vừa" : "Khó";

        /// <summary>Base armor of a hero of this class before Thể Chất (D&D: robes 10, light 11, medium 14, heavy 18; here scaled to this game's armor).</summary>
        public float BaseArmor(PlayerStats s)
        {
            switch (armor)
            {
                case ArmorKind.Light: return 2f;
                case ArmorKind.Medium: return 5f;
                case ArmorKind.Heavy: return 8f;
                case ArmorKind.Unarmored:
                    // Cuồng Chiến Binh: Thể Chất again; Võ Tăng: Thông Thái
                    var extra = id == "monk" ? CoreStat.Wisdom : CoreStat.Constitution;
                    return 1f + 0.5f * Mathf.Max(0f, s.Score(extra) - 10f);
                default: return 0f;
            }
        }
    }
}
