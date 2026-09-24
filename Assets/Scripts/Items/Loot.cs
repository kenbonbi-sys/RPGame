using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    [Serializable]
    public class LootEntry
    {
        public string itemId;
        [Range(0, 1)] public float chance = 0.5f;
        public int min = 1;
        public int max = 1;
    }

    /// <summary>Spawning items into the world.</summary>
    public static class Loot
    {
        public static void Drop(ItemDef item, int count, Vector2 at)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || db.lootPrefab == null || item == null) return;
            Vector2 land = at + UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.5f, 1.3f);
            var go = Pool.Get(db.lootPrefab, at, Quaternion.identity);
            go.GetComponent<LootPickup>().Setup(item, count, at, land);
        }

        public static void Drop(string id, int count, Vector2 at)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db != null) Drop(db.Item(id), count, at);
        }

        public static void DropCoins(Vector2 at, int coins)
        {
            // one pickup per coin looks nicer than a single stack
            for (int i = 0; i < coins; i++) Drop("coin", 1, at);
        }

        public static void Roll(List<LootEntry> table, Vector2 at)
        {
            if (table == null) return;
            foreach (var e in table)
            {
                if (UnityEngine.Random.value > e.chance) continue;
                int n = UnityEngine.Random.Range(e.min, e.max + 1);
                if (e.itemId == "coin") DropCoins(at, n);
                else Drop(e.itemId, n, at);
            }
        }
    }
}
