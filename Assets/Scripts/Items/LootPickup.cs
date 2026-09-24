using UnityEngine;

namespace RPG
{
    /// <summary>
    /// An item lying in the world: pops out in an arc, bobs with a rarity glow, then gets
    /// magnetised to the nearest hero and goes into their bag.
    /// </summary>
    public class LootPickup : MonoBehaviour
    {
        public SpriteRenderer icon;
        public SpriteRenderer glow;
        public SpriteRenderer beam;
        public SpriteRenderer shadow;
        public float magnetRadius = 2.6f;
        public float pickupRadius = 0.45f;

        ItemDef item;
        int count;
        Vector2 start, land;
        float t, popTime = 0.45f;
        float speed;
        float age;

        public void Setup(ItemDef def, int n, Vector2 from, Vector2 to)
        {
            item = def;
            count = n;
            start = from;
            land = to;
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
        }

        void Update()
        {
            age += Time.deltaTime;
            if (t < popTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / popTime);
                transform.position = Vector2.Lerp(start, land, k);
                if (icon != null) icon.transform.localPosition = new Vector3(0, 0.35f + 4f * 0.9f * k * (1 - k), 0);
                if (k >= 1f) AudioManager.Play("sfx_pickup_item", 0.25f, 0.2f, transform.position, 0.08f);
                return;
            }
            if (icon != null)
            {
                float bob = 0.35f + Mathf.Sin(age * 4f) * 0.07f;
                icon.transform.localPosition = new Vector3(0, Mathf.Round(bob * 16f) / 16f, 0);
            }
            var player = Players.Nearest(transform.position, magnetRadius);
            if (player == null) return;
            Vector2 to = (Vector2)player.transform.position - (Vector2)transform.position;
            float d = to.magnitude;
            if (age > 0.7f)
            {
                speed = Mathf.Min(speed + Time.deltaTime * 30f, 14f);
                transform.position += (Vector3)(to.normalized * Mathf.Min(d, speed * Time.deltaTime));
            }
            if (d < pickupRadius) Collect(player);
        }

        void Collect(PlayerController by)
        {
            if (by.inventory != null) by.inventory.Add(item, count);
            VFX.Spawn(item.kind == ItemKind.Currency ? "pickup_coin" : "pickup_item", transform.position + Vector3.up * 0.4f, Quaternion.identity);
            AudioManager.Play(item.kind == ItemKind.Currency ? "sfx_pickup" : "sfx_pickup_item", 0.7f, 0.08f, by.IsLocal ? (Vector3?)null : transform.position);
            Pool.Release(gameObject);
        }
    }
}
