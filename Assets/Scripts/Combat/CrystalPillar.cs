using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Cột Pha Lê in the spider queen's hall: her Tia Pha Lê bounces off it like off a mirror
    /// (<see cref="EnemyShots.Trace"/>). A hero can break it; shattered, it lets beams through and
    /// grows back after <see cref="regrowSeconds"/>. Struck while her beam is aimed at it, it
    /// shatters and the beam goes back into her (<see cref="BossSpiderQueen"/>). Online the
    /// server's pillar is the real one; every screen's copy follows its health (snapshots of
    /// <see cref="NetEntity"/>): whole or a stump.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class CrystalPillar : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<CrystalPillar> All = new System.Collections.Generic.List<CrystalPillar>();

        public SpriteRenderer sr;
        public Sprite whole, broken;
        [Tooltip("The glow of a whole pillar.")]
        public Behaviour glow;
        public float regrowSeconds = 25f;

        Health health;
        HitFlash flash;
        Collider2D[] solids;
        float regrowAt = -1f;
        bool shownBroken;

        /// <summary>When a hero last struck it (where the rules run), −99 never.</summary>
        public float StruckAt { get; private set; } = -99f;
        public bool Broken => health != null && health.IsDead;
        public Health Health => health;

        void Awake()
        {
            health = GetComponent<Health>();
            flash = GetComponentInChildren<HitFlash>();
            solids = GetComponentsInChildren<Collider2D>(true);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (GameSession.IsAuthority && Broken && regrowAt > 0f && Time.time >= regrowAt)
            {
                regrowAt = -1f;
                health.ResetHealth();
                NetCues.Vfx("crystal_burst", transform.position + Vector3.up * 1.2f, 0f, 1.2f);
                NetCues.Sound("sfx_crystal", 0.7f, 0.1f, transform.position);
            }
            // every screen: a stump while broken (a copy learns it from the snapshots)
            if (Broken != shownBroken) Show(Broken);
        }

        void Show(bool isBroken)
        {
            shownBroken = isBroken;
            if (sr != null && whole != null && broken != null) sr.sprite = isBroken ? broken : whole;
            if (glow != null) glow.enabled = !isBroken;
            foreach (var c in solids)
                if (c != null) c.enabled = !isBroken;
        }

        void OnDamaged(DamageInfo d, float amount)
        {
            if (flash != null) flash.Flash(Color.white, 0.8f, 0.1f);
            if (GameSession.IsAuthority && d.SourcePlayer != null) StruckAt = Time.time;
            if (!GameSession.HasScreen) return;
            VFX.Spawn("crystal_hit", transform.position + Vector3.up * 1.1f, Quaternion.identity);
            AudioManager.Play("sfx_crystal", 0.5f, 0.15f, transform.position);
        }

        void OnDied(DamageInfo d)
        {
            if (!GameSession.HasScreen) return;
            VFX.Spawn("crystal_shatter", transform.position + Vector3.up * 1f, Quaternion.identity);
            AudioManager.Play("sfx_crystal_break", 1f, 0.05f, transform.position);
        }

        /// <summary>Where the rules run: breaks at once (struck while the beam was on it); it grows back later.</summary>
        public void Shatter()
        {
            if (!GameSession.IsAuthority || Broken) return;
            health.Kill();
        }

        void LateUpdate()
        {
            if (GameSession.IsAuthority && Broken && regrowAt < 0f) regrowAt = Time.time + regrowSeconds;
        }

        /// <summary>The whole pillar whose collider is <paramref name="c"/>, or null.</summary>
        public static CrystalPillar Of(Collider2D c)
        {
            if (c == null) return null;
            var p = c.GetComponentInParent<CrystalPillar>();
            return p != null && !p.Broken ? p : null;
        }
    }
}
