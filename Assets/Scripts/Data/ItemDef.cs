using UnityEngine;

namespace RPG
{
    public enum ItemKind
    {
        Currency,
        Consumable,
        Material,
        Equipment,
        Quest
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic
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
    }
}
