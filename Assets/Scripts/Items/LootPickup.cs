using UnityEngine;

namespace RPG
{
    /// <summary>
    /// An item lying in the world: pops out in an arc, bobs with a rarity glow, then gets
    /// magnetised to the nearest hero and goes into their bag. Online a drop has an owner (loot
    /// is personal): only that hero draws it in, and only their screen shows it. A player's screen
    /// shows its copy flying in; the server puts the item in the bag and says when it is taken.
    /// </summary>
    public class LootPickup : MonoBehaviour
    {
        public SpriteRenderer icon;
        public SpriteRenderer glow;
        public SpriteRenderer beam;
        public SpriteRenderer shadow;
        public float magnetRadius = 2.6f;
        public float pickupRadius = 0.45f;

        /// <summary>The only hero who may take it (null: anyone).</summary>
        [System.NonSerialized] public PlayerController owner;

        ItemDef item;
        int count;
        Vector2 start, land;
        float t, popTime = 0.45f;
        float speed;
        float age;
        bool hadOwner;
        bool onlyShown;
        bool taken;

        public ItemDef Item => item;
        public int Count => count;
        public Vector2 From => start;
        public Vector2 Land => land;

        /// <param name="shownOnly">A player's copy of a drop the server keeps (it never goes into a bag here).</param>
        public void Setup(ItemDef def, int n, Vector2 from, Vector2 to, PlayerController forHero = null, bool shownOnly = false)
        {
            item = def;
            count = n;
            start = from;
            land = to;
            owner = forHero;
            hadOwner = forHero != null;
            onlyShown = shownOnly;
            taken = false;
            t = 0;
            age = 0;
            speed = 0;
            transform.position = from;
            if (icon != null) icon.sprite = def.icon;
            Color rc = def.RarityColor;
            if (glow != null) glow.color = Util.HDR(rc, 1.4f).WithAlpha(0.55f);
            if (beam != null)
            {
                beam.gameObject.SetActive(def.rarity >= ItemRarity.Uncommon);
                beam.color = Util.HDR(rc, 1.5f).WithAlpha(0.5f);
            }
            // a host's screen does not show other players' personal drops
            bool seen = owner == null || owner.IsLocal;
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = seen;
        }

        void Update()
        {
            if (taken) return;
            if (hadOwner && owner == null)
            {
                // its hero left the world
                Release();
                return;
            }
            age += Time.deltaTime;
            if (t < popTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / popTime);
                transform.position = Vector2.Lerp(start, land, k);
                if (icon != null) icon.transform.localPosition = new Vector3(0, 0.35f + 4f * 0.9f * k * (1 - k), 0);
                if (k >= 1f && Seen) AudioManager.Play("sfx_pickup_item", 0.25f, 0.2f, transform.position, 0.08f);
                return;
            }
            if (icon != null)
            {
                float bob = 0.35f + Mathf.Sin(age * 4f) * 0.07f;
                icon.transform.localPosition = new Vector3(0, Mathf.Round(bob * 16f) / 16f, 0);
            }
            var player = owner != null ? owner : Players.Nearest(transform.position, magnetRadius);
            if (player == null || player.IsDead) return;
            Vector2 to = (Vector2)player.transform.position - (Vector2)transform.position;
            float d = to.magnitude;
            if (d > magnetRadius) return;
            if (age > 0.7f)
            {
                speed = Mathf.Min(speed + Time.deltaTime * 30f, 14f);
                transform.position += (Vector3)(to.normalized * Mathf.Min(d, speed * Time.deltaTime));
            }
            if (d < pickupRadius && !onlyShown) Collect(player);
        }

        bool Seen => GameSession.HasScreen && (owner == null || owner.IsLocal);

        void Collect(PlayerController by)
        {
            if (by.inventory != null) by.inventory.Add(item, count);
            if (Seen) ShowTaken(by);
            var e = GetComponent<NetEntity>();
            if (e != null) NetWorld.Forget(e, GoneHow.Collected, NetWorld.IdOf(by));
            Release();
        }

        void ShowTaken(PlayerController by)
        {
            VFX.Spawn(item.kind == ItemKind.Currency ? "pickup_coin" : "pickup_item", transform.position + Vector3.up * 0.4f, Quaternion.identity);
            bool mine = by != null && by.IsLocal;
            AudioManager.Play(item.kind == ItemKind.Currency ? "sfx_pickup" : "sfx_pickup_item", 0.7f, 0.08f, mine ? (Vector3?)null : transform.position);
        }

        /// <summary>A player's copy: the server says the drop was taken (by that hero) or is gone.</summary>
        public void RemoteGone(PlayerController by)
        {
            if (by != null && Seen) ShowTaken(by);
            Release();
        }

        void Release()
        {
            taken = true;
            owner = null;
            hadOwner = false;
            var e = GetComponent<NetEntity>();
            if (e != null) NetWorld.Forget(e);   // no-op when already told
            Pool.Release(gameObject);
        }
    }
}
