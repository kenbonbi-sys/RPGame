using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// A hero's bag: item stacks + gold, and the gear they wear (one piece per <see cref="EquipSlot"/>,
    /// out of the bag while worn). Lives on the hero; the prefab's contents are a new character's
    /// starting kit. Putting on and taking off happen where the world's rules run (offline here,
    /// online on the server, asked with <see cref="ActKind.Equip"/>); the worn pieces travel with
    /// the bag in the "inventory" section.
    /// </summary>
    public class Inventory : MonoBehaviour, ICharacterSaveable
    {
        [Serializable]
        public class Stack
        {
            public ItemDef item;
            public int count;
        }

        public int gold;
        public int capacity = 48;
        public List<Stack> stacks = new List<Stack>();
        /// <summary>What the hero wears, by <see cref="EquipSlot"/> (null: nothing there).</summary>
        public readonly ItemDef[] equipped = new ItemDef[Gear.SlotCount];

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

        /// <summary>Whether one more <paramref name="item"/> fits in the bag.</summary>
        public bool HasRoomFor(ItemDef item)
        {
            if (item == null) return false;
            if (item.kind == ItemKind.Currency || stacks.Count < capacity) return true;
            foreach (var s in stacks)
                if (s.item == item && s.count < item.maxStack) return true;
            return false;
        }

        // ------------------------------------------------------------------ gear
        /// <summary>What is worn in <paramref name="slot"/> (null: nothing).</summary>
        public ItemDef Worn(EquipSlot slot) => slot < EquipSlot.Head || (int)slot >= equipped.Length ? null : equipped[(int)slot];

        /// <summary>
        /// Puts on <paramref name="item"/> from the bag; what was in its slot goes back into the bag.
        /// Where the world's rules run. Null when done, else why not.
        /// </summary>
        public string Equip(ItemDef item)
        {
            if (item == null || !item.IsGear) return "Không mặc được thứ này.";
            if (Count(item) <= 0) return "Không có trong túi.";
            if (Owner != null && Owner.IsDead) return "Không thể lúc này.";
            int k = (int)item.slot;
            var old = equipped[k];
            Remove(item, 1, false);
            equipped[k] = item;
            if (old != null) Add(old, 1, false);
            Changed?.Invoke();
            NetCues.Sound("sfx_ui_open", 0.6f, 0.05f, transform.position);
            return null;
        }

        /// <summary>Takes off what is worn in <paramref name="slot"/>, back into the bag. Null when done, else why not.</summary>
        public string Unequip(EquipSlot slot)
        {
            var item = Worn(slot);
            if (item == null) return "Ô này đang trống.";
            if (!HasRoomFor(item)) return "Túi đầy.";
            equipped[(int)slot] = null;
            Add(item, 1, false);
            Changed?.Invoke();
            NetCues.Sound("sfx_ui_close", 0.6f, 0.05f, transform.position);
            return null;
        }

        /// <summary>
        /// This screen's hero asks to put on <paramref name="item"/>, or (no item) to take off what
        /// is in <paramref name="slot"/>: offline done here, online the server is asked.
        /// </summary>
        public void AskEquip(ItemDef item, EquipSlot slot = EquipSlot.None)
        {
            if (!GameSession.IsAuthority)
            {
                OnlineSession.Ask(new ActRequest { kind = ActKind.Equip, value = (int)slot, text = item != null ? item.id : "" });
                return;
            }
            ApplyEquip(item != null ? item.id : null, slot);
        }

        /// <summary>The rules' side of <see cref="AskEquip"/>: an item id to put on, or none and a slot to empty.</summary>
        public string ApplyEquip(string itemId, EquipSlot slot)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            string why = !string.IsNullOrEmpty(itemId) ? Equip(db != null ? db.Item(itemId) : null) : Unequip(slot);
            if (why != null && Owner != null && Owner.health != null)
                Notify.WorldText(Owner, why, Owner.health.HeadPosition + Vector3.up * 0.4f, new Color(1f, 0.6f, 0.5f));
            return why;
        }

        // ------------------------------------------------------------------ save
        [Serializable]
        class SaveState
        {
            public int gold;
            public List<StackState> stacks = new List<StackState>();
            /// <summary>Worn gear ids by slot ("" for an empty slot).</summary>
            public List<string> equipped = new List<string>();
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
            foreach (var e in equipped) s.equipped.Add(e != null ? e.id : "");
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
            for (int i = 0; i < equipped.Length; i++)
            {
                string id = s.equipped != null && i < s.equipped.Count ? s.equipped[i] : "";
                var item = !string.IsNullOrEmpty(id) && db != null ? db.Item(id) : null;
                equipped[i] = item != null && (int)item.slot == i ? item : null;
            }
            Changed?.Invoke();
        }

        public bool Remove(ItemDef item, int n = 1) => Remove(item, n, true);

        bool Remove(ItemDef item, int n, bool tell)
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
            if (tell) Changed?.Invoke();
            return true;
        }
    }
}
