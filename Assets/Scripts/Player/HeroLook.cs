using System;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Who a hero is and how they look: their people (<see cref="RaceDef"/>), class
    /// (<see cref="ClassDef"/>), weapon and its forge level, and the choices of the character
    /// creator (skin, hair, beard, eyes, the colour of their clothes, the metal of their weapon).
    /// Saved with the character; online every screen gets it to draw them (<see cref="HeroArt"/>).
    /// Indices point into the palettes below and into the race's skins.
    /// </summary>
    [Serializable]
    public class HeroLook
    {
        public string race = "";
        public string cls = "";
        public string weapon = "";
        /// <summary>Forge level of the weapon, +0…+10 (a glow from +7).</summary>
        public int upgrade;
        public int skin;
        public int hair = 1;
        public int hairColor = 4;
        public int beard;
        public int eyes = 1;
        /// <summary>The outfit's main colour (<see cref="Cloths"/>); −1: the class's own.</summary>
        public int cloth = -1;
        /// <summary>The weapon's metal (<see cref="Metals"/>).</summary>
        public int metal;
        /// <summary>Leaves off the class's hood or hat, showing the hair.</summary>
        public bool bareHead;
        /// <summary>The spells of the schools (Băng, Lôi, Ám) learned from Bí Kíp: ability ids (<see cref="Spellbook"/>).</summary>
        public string[] spells = new string[0];
        /// <summary>
        /// The skill bar's W E R A S D (slots 1–6): a learned spell put there, or "" for the class's
        /// own (<see cref="Spellbook"/>). Every screen sets the bar from it, so another player's
        /// cast shows the right skill.
        /// </summary>
        public string[] bar = new string[0];

        public bool HasClass => !string.IsNullOrEmpty(cls);

        public HeroLook Clone()
        {
            var c = (HeroLook)MemberwiseClone();
            c.spells = spells != null ? (string[])spells.Clone() : new string[0];
            c.bar = bar != null ? (string[])bar.Clone() : new string[0];
            return c;
        }

        /// <summary>What the look draws (the spells and the bar left out): the key of the drawn sheets.</summary>
        public string ArtKey()
        {
            var c = (HeroLook)MemberwiseClone();
            c.spells = null;
            c.bar = null;
            return JsonUtility.ToJson(c);
        }

        public string ToJson() => JsonUtility.ToJson(this);

        public static HeroLook FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return new HeroLook();
            try { return JsonUtility.FromJson<HeroLook>(json) ?? new HeroLook(); }
            catch (ArgumentException) { return new HeroLook(); }
        }

        public bool SameAs(HeroLook o) => o != null && ToJson() == o.ToJson();

        // ------------------------------------------------------------------ palettes
        public struct Swatch
        {
            public string name;
            public Color color;
            public Swatch(string name, string hex)
            {
                this.name = name;
                ColorUtility.TryParseHtmlString(hex, out color);
            }
        }

        public static readonly string[] HairStyles = { "Ngắn", "Dài", "Đuôi Ngựa", "Búi Tó", "Dựng", "Tết Đôi", "Trọc" };
        public const int Bald = 6;

        public static readonly string[] Beards = { "Không", "Râu Ngắn", "Râu Dài" };

        public static readonly Swatch[] HairColors =
        {
            new Swatch("Đen", "#2c2432"), new Swatch("Nâu Sẫm", "#4e3024"), new Swatch("Nâu", "#7e4e2c"),
            new Swatch("Đỏ Hung", "#b0482c"), new Swatch("Đỏ", "#c23a45"), new Swatch("Vàng", "#e2b446"),
            new Swatch("Bạch Kim", "#ece2c4"), new Swatch("Bạc", "#b4bac8"), new Swatch("Xanh Lam", "#3c6cd2"),
            new Swatch("Xanh Lục", "#3c9a5c"), new Swatch("Tím", "#8c4cc8"), new Swatch("Hồng", "#e274aa"),
        };

        public static readonly Swatch[] EyeColors =
        {
            new Swatch("Nâu", "#4a2c1c"), new Swatch("Đen", "#241826"), new Swatch("Xanh Lam", "#2c62c0"),
            new Swatch("Xanh Lục", "#2e9038"), new Swatch("Hổ Phách", "#d0901e"), new Swatch("Tím", "#7038b0"),
            new Swatch("Đỏ", "#d02434"), new Swatch("Vàng", "#f0c030"),
        };

        public static readonly Swatch[] Cloths =
        {
            new Swatch("Đỏ", "#b52a34"), new Swatch("Xanh Dương", "#2c58b0"), new Swatch("Xanh Lá", "#2e8a36"),
            new Swatch("Tím", "#6232a0"), new Swatch("Cam", "#d2641e"), new Swatch("Vàng", "#c8a02a"),
            new Swatch("Đen", "#2e2a38"), new Swatch("Trắng", "#d8dce8"), new Swatch("Nâu", "#74513a"),
            new Swatch("Hồng", "#c8508a"), new Swatch("Xanh Ngọc", "#1e8a8a"), new Swatch("Xám", "#6c7088"),
        };

        public static readonly Swatch[] Metals =
        {
            new Swatch("Thép", "#a4acc6"), new Swatch("Đồng", "#c87a3a"), new Swatch("Vàng", "#e8b634"),
            new Swatch("Hắc Thiết", "#54546c"), new Swatch("Pha Lê", "#6ec4ec"), new Swatch("Ngọc Lục", "#50b878"),
            new Swatch("Huyết Thạch", "#c83040"),
        };

        /// <summary>Long Duệ: the element of an ancestry by the colour of its scales (the race's skins, in order).</summary>
        public static readonly DamageType[] DraconicElements =
        {
            DamageType.Fire,        // Đỏ
            DamageType.Fire,        // Vàng Kim
            DamageType.Lightning,   // Lam
            DamageType.Lightning,   // Đồng Thiếc
            DamageType.Ice,         // Trắng
            DamageType.Ice,         // Bạc
            DamageType.Poison,      // Lục
            DamageType.Poison,      // Đen
        };

        public static readonly string[] DraconicNames = { "Rồng Đỏ", "Rồng Vàng Kim", "Rồng Lam", "Rồng Đồng Thiếc", "Rồng Trắng", "Rồng Bạc", "Rồng Lục", "Rồng Đen" };

        public DamageType DraconicElement => DraconicElements[Mathf.Clamp(skin, 0, DraconicElements.Length - 1)];

        public static Color Pick(Swatch[] swatches, int i) => swatches[Mathf.Clamp(i, 0, swatches.Length - 1)].color;
    }
}
