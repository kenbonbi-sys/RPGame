using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>Player bag: item stacks + gold.</summary>
    public class Inventory : MonoBehaviour
    {
        public static Inventory I { get; private set; }

        [Serializable]
        public class Stack
        {
            public ItemDef item;
            public int count;
        }

        public int gold;
        public int capacity = 20;
        public List<Stack> stacks = new List<Stack>();

        public event Action Changed;

        void Awake() => I = this;

        public int Count(ItemDef item)
        {
            if (item == null) return 0;
            int n = 0;
            foreach (var s in stacks)
                if (s.item == item) n += s.count;
            return n;
        }

        public int Count(string id)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            return db != null ? Count(db.Item(id)) : 0;
        }

        public bool Add(ItemDef item, int n = 1, bool announce = true)
        {
            if (item == null || n <= 0) return false;
            if (item.kind == ItemKind.Currency)
            {
                gold += n * Mathf.Max(1, item.value);
                Changed?.Invoke();
                if (announce) GameEvents.RaiseItemPicked(item, n);
                return true;
            }
            int left = n;
            foreach (var s in stacks)
            {
                if (s.item != item || s.count >= item.maxStack) continue;
                int add = Mathf.Min(left, item.maxStack - s.count);
                s.count += add;
                left -= add;
                if (left <= 0) break;
            }
            while (left > 0 && stacks.Count < capacity)
            {
                int add = Mathf.Min(left, item.maxStack);
                stacks.Add(new Stack { item = item, count = add });
                left -= add;
            }
            Changed?.Invoke();
            if (announce) GameEvents.RaiseItemPicked(item, n - left);
            return left == 0;
        }

        public bool Remove(ItemDef item, int n = 1)
        {
            if (Count(item) < n) return false;
            for (int i = stacks.Count - 1; i >= 0 && n > 0; i--)
            {
                var s = stacks[i];
                if (s.item != item) continue;
                int take = Mathf.Min(n, s.count);
                s.count -= take;
                n -= take;
                if (s.count <= 0) stacks.RemoveAt(i);
            }
            Changed?.Invoke();
            return true;
        }
    }
}
