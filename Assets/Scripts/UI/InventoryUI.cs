using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RPG
{
    /// <summary>"Túi Đồ" — the bag window (B / I).</summary>
    public class InventoryUI : UIPanel
    {
        public RectTransform grid;
        public InventorySlotUI slotPrefab;
        public TextMeshProUGUI goldText;
        public TextMeshProUGUI countText;

        readonly List<InventorySlotUI> slots = new List<InventorySlotUI>();
        bool hooked;

        protected override void OnShow() => Refresh();

        protected override void Update()
        {
            base.Update();
            if (!hooked && Inventory.I != null)
            {
                hooked = true;
                Inventory.I.Changed += Refresh;
            }
        }

        void OnDestroy()
        {
            if (hooked && Inventory.I != null) Inventory.I.Changed -= Refresh;
        }

        void Refresh()
        {
            var inv = Inventory.I;
            if (inv == null || slotPrefab == null || grid == null) return;
            while (slots.Count < inv.capacity)
            {
                var s = Instantiate(slotPrefab, grid);
                s.gameObject.SetActive(true);
                slots.Add(s);
            }
            for (int i = 0; i < slots.Count; i++)
                slots[i].Set(i < inv.stacks.Count ? inv.stacks[i] : null);
            if (goldText != null) goldText.text = $"<color=#ffd84a>{inv.gold}</color> vàng";
            if (countText != null) countText.text = $"{inv.stacks.Count}/{inv.capacity}";
        }
    }
}
