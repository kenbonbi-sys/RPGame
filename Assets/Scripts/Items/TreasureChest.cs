using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The chest a fallen boss leaves in the middle of its arena, holding a hero's share of its loot.
    /// Walking up to it opens it: the lid flies back and the loot bursts out around it. Online every
    /// hero with the kill gets a chest of their own that only their screen shows (loot is personal),
    /// so heroes standing together each open their own. A chest nobody comes for opens by itself
    /// after <see cref="WaitsFor"/> seconds; one whose hero left the world goes with them.
    /// </summary>
    public class TreasureChest : MonoBehaviour
    {
        public SpriteRenderer body;
        public SpriteRenderer glow;
        public Sprite closedSprite;
        public Sprite openSprite;
        public float openRadius = 1.3f;

        /// <summary>Seconds a chest waits for its hero before opening on its own.</summary>
        public const float WaitsFor = 150f;
        /// <summary>Seconds an opened chest stays before it fades away.</summary>
        const float Linger = 3f;

        /// <summary>The only hero who can open it (null: anyone, offline).</summary>
        [System.NonSerialized] public PlayerController owner;

        public bool Opened { get; private set; }
        public IReadOnlyList<(ItemDef item, int count)> Contents => contents;

        readonly List<(ItemDef item, int count)> contents = new List<(ItemDef, int)>();
        bool personal, shownOnly;
        float readyAt, waitUntil;

        /// <summary>A chest for <paramref name="forHero"/> (null: anyone) holding <paramref name="items"/>; the server's or the offline game's.</summary>
        public void Fill(PlayerController forHero, List<(ItemDef item, int count)> items)
        {
            owner = forHero;
            personal = forHero != null;
            shownOnly = false;
            contents.Clear();
            if (items != null) contents.AddRange(items);
            Reset();
        }

        /// <summary>A player's copy of their chest: the server opens it and says when.</summary>
        public void Show()
        {
            owner = Players.Local;
            personal = false;
            shownOnly = true;
            contents.Clear();
            Reset();
        }

        void Reset()
        {
            Opened = false;
            readyAt = Time.time + 0.8f;   // lands and settles before it can open
            waitUntil = Time.time + WaitsFor;
            if (body != null)
            {
                body.sprite = closedSprite;
                body.color = Color.white;
            }
            if (glow != null) glow.enabled = true;
            if (GameSession.HasScreen)
            {
                VFX.Spawn("chest_appear", transform.position, Quaternion.identity);
                AudioManager.Play("sfx_chest_appear", 0.8f, 0.05f, transform.position);
            }
        }

        void Update()
        {
            if (Opened || shownOnly || !GameSession.IsAuthority) return;
            if (personal && owner == null)
            {
                // its hero left the world
                Opened = true;
                if (GameSession.Serving) NetWorld.Forget(GetComponent<NetEntity>());
                Destroy(gameObject);
                return;
            }
            if (Time.time < readyAt) return;
            PlayerController by = null;
            if (owner != null)
            {
                if (!owner.IsDead && Vector2.Distance(owner.transform.position, transform.position) <= openRadius) by = owner;
            }
            else
            {
                foreach (var p in Players.All)
                    if (p != null && !p.IsDead && Vector2.Distance(p.transform.position, transform.position) <= openRadius) { by = p; break; }
            }
            if (by != null || Time.time >= waitUntil) Open(by);
        }

        /// <summary>Opens it (the server's or the offline game's): the loot bursts out for its hero.</summary>
        void Open(PlayerController by)
        {
            Opened = true;
            Vector2 at = (Vector2)transform.position + Vector2.up * 0.2f;
            foreach (var (item, count) in contents)
            {
                if (item == null) continue;
                if (item.id == "coin")
                    for (int i = 0; i < count; i++) Loot.Drop(item, 1, at, owner);   // one pickup per coin
                else Loot.Drop(item, count, at, owner);
            }
            contents.Clear();
            if (GameSession.Serving) NetWorld.Forget(GetComponent<NetEntity>(), GoneHow.Collected, NetWorld.IdOf(by));
            ShowOpened();
        }

        /// <summary>The lid flies back, gold light and a chime; then it fades away. On every screen that shows it.</summary>
        public void ShowOpened()
        {
            Opened = true;
            if (GameSession.HasScreen)
            {
                if (body != null) body.sprite = openSprite;
                VFX.Spawn("chest_open", transform.position + Vector3.up * 0.5f, Quaternion.identity);
                AudioManager.Play("sfx_chest_open", 0.9f, 0.05f, transform.position);
            }
            StartCoroutine(FadeAway());
        }

        IEnumerator FadeAway()
        {
            yield return new WaitForSeconds(Linger);
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                if (body != null) body.color = new Color(1f, 1f, 1f, 1f - t);
                if (glow != null) glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, (1f - t) * 0.6f);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>The nearest spot to <paramref name="at"/> out of the water (the snake mother's arena is a pond).</summary>
        static Vector2 DryLand(Vector2 at)
        {
            var zone = ZoneRoot.Current;
            if (zone == null || (!zone.IsWater(at) && !zone.IsWall(at))) return at;
            for (float r = 1f; r <= 12f; r += 1f)
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI / 8f;
                    var p = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (!zone.IsWater(p) && !zone.IsWall(p)) return p;
                }
            return at;
        }

        /// <summary>
        /// Leaves a chest at <paramref name="at"/> for each hero in <paramref name="credited"/> (online),
        /// or one for anyone (offline), holding that hero's roll of <paramref name="table"/> and <paramref name="coins"/> coins.
        /// </summary>
        public static void Leave(Vector2 at, List<LootEntry> table, int coins, IReadOnlyList<PlayerController> credited)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || !GameSession.IsAuthority) return;
            at = DryLand(at);
            foreach (var owner in Loot.Owners(credited))
            {
                var items = Loot.RollList(table, owner);
                if (coins > 0) items.Add((db.Item("coin"), Loot.Coins(coins, owner)));
                if (db.chestPrefab == null)
                {
                    // no chest in this build: the loot drops as it always did
                    foreach (var (item, count) in items) Loot.Drop(item, count, at, owner);
                    continue;
                }
                var parent = ZoneRoot.Current != null ? ZoneRoot.Current.transform : null;
                var go = Instantiate(db.chestPrefab, at, Quaternion.identity, parent);
                var chest = go.GetComponent<TreasureChest>();
                chest.Fill(owner, items);
                if (GameSession.Serving) NetWorld.ChestLeft(chest);
            }
        }
    }
}
