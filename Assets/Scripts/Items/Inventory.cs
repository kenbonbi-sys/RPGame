using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>A hero's bag: item stacks + gold. Lives on the hero; the prefab's contents are a new character's starting kit.</summary>
    public class Inventory : MonoBehaviour, ICharacterSaveable
    {
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

        /// <summary>Tells the bag's watchers it changed (gold spent outside Add/Remove: the forge).</summary>
        public void NotifyChanged() => Changed?.Invoke();

        /// <summary>The hero carrying the bag.</summary>
        public PlayerController Owner { get; private set; }

        void Awake()
        {
            Owner = GetComponent<PlayerController>();
            SaveRegistry.Register(this);
        }

        void OnDestroy() => SaveRegistry.Unregister(this);


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
                if (announce) Notify.ItemPicked(Owner, item, n);   // "Nhận được…" on its owner's screen only
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
            if (announce) Notify.ItemPicked(Owner, item, n - left);
            return left == 0;
        }

        // ------------------------------------------------------------------ save
        [Serializable]
        class SaveState
        {
            public int gold;
            public List<StackState> stacks = new List<StackState>();
        }

        [Serializable]
        class StackState
        {
            public string id;
            public int count;
        }

        public string SaveKey => "inventory";

        public string CaptureState()
        {
            var s = new SaveState { gold = gold };
            foreach (var st in stacks)
                if (st.item != null && st.count > 0) s.stacks.Add(new StackState { id = st.item.id, count = st.count });
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<SaveState>(json);
            var db = GameManager.I != null ? GameManager.I.db : null;
            gold = s.gold;
            stacks.Clear();
            foreach (var st in s.stacks)
            {
                var item = db != null ? db.Item(st.id) : null;
                if (item == null)
                {
                    Debug.LogWarning("[Save] Unknown item id: " + st.id);
                    continue;
                }
                stacks.Add(new Stack { item = item, count = st.count });
            }
            Changed?.Invoke();
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
