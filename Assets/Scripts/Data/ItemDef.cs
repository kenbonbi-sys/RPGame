using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RPG
{
    public enum ItemKind
    {
        Currency,
        Consumable,
        Material,
        Equipment,
        Quest,
        /// <summary>A Bí Kíp: reading it teaches a spell of the Sách Chiêu (<see cref="Spellbook"/>).</summary>
        Tome
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic
    }

    /// <summary>
    /// Where a piece of gear is worn (the slots around the hero on the character screen). The
    /// weapon is not an item: it is the hero's own, shaped at the forge (<see cref="HeroLook"/>).
    /// </summary>
    public enum EquipSlot
    {
        None = -1,
        Head = 0,
        Body = 1,
        Feet = 2,
        Offhand = 3,
        Ring = 4
    }

    /// <summary>So many of an item (a recipe's part).</summary>
    [Serializable]
    public struct ItemCount
    {
        public string id;
        public int count;

        public ItemCount(string id, int count)
        {
            this.id = id;
            this.count = count;
        }
    }

    [CreateAssetMenu(menuName = "RPG/Item")]
    public class ItemDef : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public ItemKind kind = ItemKind.Material;
        public ItemRarity rarity = ItemRarity.Common;
        public int maxStack = 99;
        public int value = 1;

        [Header("Consumable")]
        public float healAmount;
        public float energyAmount;
        public bool cleanse;

        [Header("Gear")]
        [Tooltip("Where it is worn (None: not gear).")]
        public EquipSlot slot = EquipSlot.None;
        [Tooltip("What wearing it adds to the hero's numbers.")]
        public StatModifier[] bonuses = new StatModifier[0];

        [Header("Forge (Lò Rèn, tab Chế Tạo)")]
        [Tooltip("Gold the smith asks to make it; 0 with no parts: the smith does not make it.")]
        public int craftGold;
        public ItemCount[] craftItems = new ItemCount[0];

        public bool IsGear => slot != EquipSlot.None;
        public bool Craftable => craftGold > 0 || (craftItems != null && craftItems.Length > 0);

        public Color RarityColor
        {
            get
            {
                switch (rarity)
                {
                    case ItemRarity.Uncommon: return new Color(0.45f, 0.95f, 0.45f);
                    case ItemRarity.Rare: return new Color(0.4f, 0.7f, 1f);
                    case ItemRarity.Epic: return new Color(0.85f, 0.5f, 1f);
                    default: return new Color(0.95f, 0.92f, 0.85f);
                }
            }
        }

        public string RarityName
        {
            get
            {
                switch (rarity)
                {
                    case ItemRarity.Uncommon: return "Khá";
                    case ItemRarity.Rare: return "Hiếm";
                    case ItemRarity.Epic: return "Sử Thi";
                    default: return "Thường";
                }
            }
        }

        /// <summary>What sort of thing it is, as the tooltip's second line says it.</summary>
        public string KindName
        {
            get
            {
                if (IsGear) return "Trang bị · " + Gear.SlotName(slot);
                switch (kind)
                {
                    case ItemKind.Consumable: return "Tiêu hao";
                    case ItemKind.Material: return "Nguyên liệu";
                    case ItemKind.Quest: return "Vật phẩm nhiệm vụ";
                    case ItemKind.Currency: return "Tiền";
                    case ItemKind.Tome: return "Bí Kíp";
                    default: return "Trang bị";
                }
            }
        }

        /// <summary>"+3 Giáp", "+15 Máu tối đa"…, one bonus a line (empty: none).</summary>
        public string BonusText(string separator = "\n")
        {
            if (bonuses == null || bonuses.Length == 0) return "";
            var sb = new StringBuilder();
            foreach (var b in bonuses)
            {
                if (sb.Length > 0) sb.Append(separator);
                sb.Append(Gear.ModifierText(b));
            }
            return sb.ToString();
        }
    }

    /// <summary>Names and numbers of gear: the slots, and what a bonus reads like.</summary>
    public static class Gear
    {
        public const int SlotCount = 5;

        public static readonly EquipSlot[] Slots = { EquipSlot.Head, EquipSlot.Body, EquipSlot.Feet, EquipSlot.Offhand, EquipSlot.Ring };

        public static string SlotName(EquipSlot s)
        {
            switch (s)
            {
                case EquipSlot.Head: return "Mũ";
                case EquipSlot.Body: return "Giáp";
                case EquipSlot.Feet: return "Giày";
                case EquipSlot.Offhand: return "Tay Phụ";
                case EquipSlot.Ring: return "Nhẫn";
                default: return "";
            }
        }

        /// <summary>Stats kept as shares (0.05 = 5%).</summary>
        public static bool IsShare(StatId s)
        {
            switch (s)
            {
                case StatId.CritChance:
                case StatId.CritDamage:
                case StatId.AttackSpeed:
                case StatId.DashCooldownReduction:
                case StatId.ElementalResist:
                case StatId.CooldownReduction:
                case StatId.GoldFind:
                case StatId.HealingPower:
                case StatId.PoiseDamage:
                case StatId.DamageDealt:
                    return true;
                default:
                    return false;
            }
        }

        public static string StatName(StatId s)
        {
            switch (s)
            {
                case StatId.Strength: return "Sức Mạnh";
                case StatId.Dexterity: return "Khéo Léo";
                case StatId.Constitution: return "Thể Chất";
                case StatId.Intelligence: return "Trí Tuệ";
                case StatId.Wisdom: return "Thông Thái";
                case StatId.Charisma: return "Sức Hút";
                case StatId.MaxHp: return "Máu tối đa";
                case StatId.MaxEnergy: return "Năng lượng";
                case StatId.Armor: return "Giáp";
                case StatId.PhysicalAttack: return "Công vật lý";
                case StatId.MagicAttack: return "Công phép";
                case StatId.CritChance: return "Chí mạng";
                case StatId.CritDamage: return "Sát thương chí mạng";
                case StatId.AttackSpeed: return "Tốc đánh";
                case StatId.DashCooldownReduction: return "Hồi Lướt nhanh";
                case StatId.ElementalResist: return "Kháng hệ";
                case StatId.PoiseDamage: return "Trấn Áp";
                case StatId.DamageDealt: return "Sát thương";
                case StatId.CooldownReduction: return "Giảm hồi chiêu";
                case StatId.HealingPower: return "Hồi máu";
                case StatId.GoldFind: return "Vàng nhặt thêm";
                default: return s.ToString();
            }
        }

        /// <summary>"+3 Giáp", "+5% Tốc đánh".</summary>
        public static string ModifierText(StatModifier m)
        {
            bool share = IsShare(m.stat) || m.kind != ModKind.Flat;
            float v = share ? m.value * 100f : m.value;
            string num = Mathf.Abs(v).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            return $"{(v >= 0 ? "+" : "−")}{num}{(share ? "%" : "")} {StatName(m.stat)}";
        }

        /// <summary>Everything a set of worn items adds, with <paramref name="source"/> as their source.</summary>
        public static void Collect(IReadOnlyList<ItemDef> worn, object source, List<StatModifier> into)
        {
            into.Clear();
            if (worn == null) return;
            foreach (var item in worn)
            {
                if (item == null || item.bonuses == null) continue;
                foreach (var b in item.bonuses)
                {
                    var m = b;
                    m.source = source;
                    into.Add(m);
                }
            }
        }
    }
}
