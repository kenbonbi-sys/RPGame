using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// What an item's tooltip says, the same in the bag, on the hero and at the forge: its rarity
    /// and sort, its words, what it adds when worn (and against what is worn now), what the smith
    /// uses it for, and what a click does.
    /// </summary>
    public static class ItemTips
    {
        public const string Good = "#8cf08c";
        public const string Bad = "#ff8c73";
        public const string Gold = "#ffe07a";
        public const string Muted = "#b8b0c8";

        public static (string title, string body, Color color) For(ItemDef item, PlayerController hero, string hint = null, int count = 0)
        {
            if (item == null) return (null, null, Color.white);
            var sb = new StringBuilder();
            sb.Append($"<color={Muted}><size=85%>{item.RarityName} · {item.KindName}</size></color>");
            if (!string.IsNullOrEmpty(item.description)) sb.Append('\n').Append(item.description);
            if (item.IsGear)
            {
                sb.Append($"\n\n<color={Good}>").Append(item.BonusText()).Append("</color>");
                var worn = hero != null && hero.inventory != null ? hero.inventory.Worn(item.slot) : null;
                if (worn != null && worn != item)
                    sb.Append($"\n<color={Muted}><size=85%>Đang mặc: {worn.displayName} ({worn.BonusText(", ")})</size></color>");
            }
            var spell = Spellbook.OfTome(item);
            if (spell != null) sb.Append('\n').Append(TomeText(spell, hero));
            string uses = Uses(item);
            if (uses != null) sb.Append($"\n\n<color={Muted}><size=85%>Lò Rèn cần: {uses}</size></color>");
            if (count > 1) sb.Append($"\n<color={Muted}><size=85%>Đang có: {count}</size></color>");
            if (!string.IsNullOrEmpty(hint)) sb.Append($"\n\n<color=#9ad06a>{hint}</color>");
            return (item.displayName, sb.ToString(), item.RarityColor);
        }

        /// <summary>A Bí Kíp's spell: what it does, its school, who may learn it and whether this hero may.</summary>
        static string TomeText(Spellbook.Entry e, PlayerController hero)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            var a = db != null ? db.Ability(e.id) : null;
            var sb = new StringBuilder();
            string color = "#" + ColorUtility.ToHtmlStringRGB(Spellbook.SchoolColor(e.school));
            sb.Append($"\n<color={color}><b>{(a != null ? a.displayName : e.id)}</b> · hệ {Spellbook.SchoolName(e.school)}</color>");
            if (a != null) sb.Append('\n').Append(a.Tooltip());
            sb.Append($"\n<color={Muted}><size=85%>Lớp học được: {Spellbook.ClassNames(e)}</size></color>");
            string why = Spellbook.WhyNotLearn(hero, e);
            sb.Append(why == null ? $"\n<color={Good}>Chuột phải để học.</color>" : $"\n<color={Bad}>{why}</color>");
            return sb.ToString();
        }

        /// <summary>"Nâng +8 (6), Mũ Pha Lê (8)": where the forge asks for <paramref name="item"/> (null: nowhere).</summary>
        public static string Uses(ItemDef item)
        {
            if (item == null || item.kind == ItemKind.Currency) return null;
            var parts = new List<string>();
            for (int lv = 1; lv <= WeaponKinds.MaxUpgrade; lv++)
                foreach (var (id, n) in Forge.UpgradeCost(lv).items)
                    if (id == item.id) parts.Add($"Nâng +{lv} ({n})");
            for (int m = 0; m < HeroLook.Metals.Length; m++)
                foreach (var (id, n) in Forge.MetalCost(m).items)
                    if (id == item.id) parts.Add($"Kim loại {HeroLook.Metals[m].name} ({n})");
            foreach (var r in Forge.Recipes())
                if (r.craftItems != null)
                    foreach (var c in r.craftItems)
                        if (c.id == item.id) parts.Add($"{r.displayName} ({c.count})");
            if (parts.Count == 0) return null;
            if (parts.Count > 5) parts.RemoveRange(5, parts.Count - 5);
            return string.Join(", ", parts);
        }
    }
}
