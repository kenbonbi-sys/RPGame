using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPG
{
    public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Image icon;
        public Image rarity;
        public TextMeshProUGUI count;
        public Image highlight;

        Inventory.Stack stack;

        public void Set(Inventory.Stack s)
        {
            stack = s;
            bool has = s != null && s.item != null;
            if (icon != null)
            {
                icon.enabled = has;
                if (has) icon.sprite = s.item.icon;
            }
            if (count != null) count.text = has && s.count > 1 ? s.count.ToString() : "";
            if (rarity != null)
            {
                rarity.enabled = has && s.item.rarity > ItemRarity.Common;
                if (has) rarity.color = s.item.RarityColor.WithAlpha(0.35f);
            }
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (highlight != null) highlight.enabled = true;
            if (stack == null || stack.item == null || HUD.I == null || HUD.I.tooltip == null) return;
            var it = stack.item;
            string body = $"<color=#b8b0c8><size=85%>{it.RarityName}</size></color>\n{it.description}";
            if (it.kind == ItemKind.Consumable) body += "\n\n<color=#9ad06a>Nhấp chuột phải hoặc phím 1/2/3 để dùng</color>";
            HUD.I.tooltip.Show(it.displayName, body, it.RarityColor);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (highlight != null) highlight.enabled = false;
            if (HUD.I != null && HUD.I.tooltip != null) HUD.I.tooltip.Hide();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (stack == null || stack.item == null || e.button != PointerEventData.InputButton.Right) return;
            var p = GameManager.I != null ? GameManager.I.player : null;
            if (p == null) return;
            int idx = System.Array.IndexOf(p.potionIds, stack.item.id);
            if (idx >= 0) p.UsePotion(idx);
        }
    }
}
