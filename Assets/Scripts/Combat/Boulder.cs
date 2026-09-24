using UnityEngine;

namespace RPG
{
    /// <summary>
    /// "★ Tảng Đá Lớn" — a big rock (thrown by the boss or placed in the arena).
    /// Blocks movement, can be broken by the player, and stuns the boss if it lands on it.
    /// Online the server's rock is the real one; every screen shows its copy and breaks it when
    /// the server's breaks.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Boulder : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<Boulder> All = new System.Collections.Generic.List<Boulder>();

        public SpriteRenderer sr;
        public float dropChance = 0.35f;
        Health health;
        NameplateUI plate;
        HitFlash flash;

        void Awake()
        {
            health = GetComponent<Health>();
            flash = GetComponentInChildren<HitFlash>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnEnable()
        {
            All.Add(this);
            if (health != null) health.ResetHealth();
            if (HUD.I != null && plate == null)
                plate = HUD.I.CreateNameplate(transform, "Tảng Đá Lớn", true, new Color(1f, 0.72f, 0.25f), health, 1.7f);
        }

        void OnDisable()
        {
            All.Remove(this);
            if (plate != null)
            {
                plate.Release();
                plate = null;
            }
        }

        void OnDestroy()
        {
            var e = GetComponent<NetEntity>();
            if (e != null) NetWorld.Forget(e, GoneHow.Shattered);
        }

        /// <summary>Every screen: a hit on the rock (the server's, or this machine's own).</summary>
        void OnDamaged(DamageInfo d, float amount)
        {
            if (flash != null) flash.Flash(Color.white, 0.8f, 0.1f);
            if (!GameSession.HasScreen) return;
            VFX.Spawn("rock_chips", transform.position + Vector3.up * 0.6f, Quaternion.identity);
            AudioManager.Play("sfx_hit", 0.4f, 0.1f, transform.position);
        }

        void OnDied(DamageInfo d)
        {
            if (GameSession.IsAuthority) Shatter(true, d.SourcePlayer);
        }

        /// <summary>Breaks the rock with debris; optionally drops a small reward for whoever broke it.</summary>
        public void Shatter(bool allowDrop, PlayerController by = null)
        {
            NetCues.Vfx("boulder_break", transform.position + Vector3.up * 0.5f);
            NetCues.Sound("sfx_boulder_break", 1f, 0.05f, transform.position);
            NetCues.Shake(0.25f, transform.position);
            if (allowDrop && Random.value < dropChance) Loot.DropCoins(transform.position, Random.Range(1, 4), by != null ? new[] { by } : null);
            Destroy(gameObject);
        }

        public static Boulder Nearest(Vector2 p, float maxDist)
        {
            Boulder best = null;
            float bd = maxDist * maxDist;
            foreach (var b in All)
            {
                if (b == null) continue;
                float d = ((Vector2)b.transform.position - p).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    best = b;
                }
            }
            return best;
        }
    }
}
