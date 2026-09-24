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

    /// <summary>
    /// Spawning items into the world. Online loot is personal (Docs/KeHoach-Online.md): every
    /// hero who shares a kill rolls the table for themselves, and only they see and pick up what
    /// dropped for them, so nobody takes anybody's drops.
    /// </summary>
    public static class Loot
    {
        /// <summary>Drops <paramref name="count"/> × <paramref name="item"/>; <paramref name="owner"/> alone may take it (null: anyone).</summary>
        public static void Drop(ItemDef item, int count, Vector2 at, PlayerController owner = null)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || db.lootPrefab == null || item == null || !GameSession.IsAuthority) return;
            Vector2 land = at + UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.5f, 1.3f);
            var go = Pool.Get(db.lootPrefab, at, Quaternion.identity);
            var pickup = go.GetComponent<LootPickup>();
            pickup.Setup(item, count, at, land, owner, false);
            if (GameSession.Serving) NetWorld.LootDropped(pickup);
        }

        public static void Drop(string id, int count, Vector2 at, PlayerController owner = null)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db != null) Drop(db.Item(id), count, at, owner);
        }

        /// <summary>Coins, one pickup per coin; online each hero in <paramref name="credited"/> gets their own.</summary>
        public static void DropCoins(Vector2 at, int coins, IReadOnlyList<PlayerController> credited = null)
        {
            foreach (var owner in Owners(credited))
            {
                int n = Coins(coins, owner);
                for (int i = 0; i < n; i++) Drop("coin", 1, at, owner);   // one pickup per coin looks nicer than a single stack
            }
        }

        /// <summary>Rolls a loot table at <paramref name="at"/>; online once for each hero in <paramref name="credited"/>.</summary>
        public static void Roll(List<LootEntry> table, Vector2 at, IReadOnlyList<PlayerController> credited = null)
        {
            if (table == null) return;
            foreach (var owner in Owners(credited))
            {
                foreach (var e in table)
                {
                    if (UnityEngine.Random.value > e.chance) continue;
                    int n = UnityEngine.Random.Range(e.min, e.max + 1);
                    if (e.itemId == "coin")
                        for (int i = Coins(n, owner); i > 0; i--) Drop("coin", 1, at, owner);
                    else Drop(e.itemId, n, at, owner);
                }
            }
        }

        /// <summary>One roll of a loot table, kept instead of dropped (a boss's <see cref="TreasureChest"/> holds it).</summary>
        public static List<(ItemDef item, int count)> RollList(List<LootEntry> table, PlayerController owner = null)
        {
            var result = new List<(ItemDef, int)>();
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (table == null || db == null) return result;
            foreach (var e in table)
            {
                if (UnityEngine.Random.value > e.chance) continue;
                var item = db.Item(e.itemId);
                int n = UnityEngine.Random.Range(e.min, e.max + 1);
                if (e.itemId == "coin") n = Coins(n, owner);
                if (item != null && n > 0) result.Add((item, n));
            }
            return result;
        }

        /// <summary>A hero's share of <paramref name="n"/> coins with their Sức Hút (offline: the hero playing).</summary>
        public static int Coins(int n, PlayerController owner)
        {
            var hero = owner != null ? owner : Players.Local;
            float find = hero != null && hero.stats != null ? hero.stats.GoldFind : 0f;
            return Mathf.Max(0, Mathf.RoundToInt(n * (1f + find)));
        }

        static readonly PlayerController[] Anyone = { null };

        /// <summary>Who drops are for: offline (or a kill nobody is credited with) one drop anyone takes; online one per hero.</summary>
        public static IReadOnlyList<PlayerController> Owners(IReadOnlyList<PlayerController> credited)
        {
            if (!GameSession.Online || credited == null || credited.Count == 0) return Anyone;
            return credited;
        }
    }
}
