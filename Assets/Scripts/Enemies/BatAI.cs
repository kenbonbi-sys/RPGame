using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Dơi Pha Lê (Hang Pha Lê): a crystal bat. It flits and circles a hero and darts through
    /// them like the swamp's dragonflies (<see cref="DragonflyAI"/>), but in swarms, and its bite
    /// drinks: it heals part of what it deals. It fears light: a bright hit (fire, lightning, holy
    /// light) scatters it for a moment, whatever it was about to do.
    /// </summary>
    public class BatAI : DragonflyAI
    {
        [Header("Bat")]
        [Tooltip("Share of its bite's damage it heals.")]
        [Range(0f, 1f)] public float drink = 0.5f;
        [Tooltip("How long a bright hit sends it flapping away.")]
        public float scatterSeconds = 1.4f;

        /// <summary>Whether a hit of this kind is bright enough to scatter a bat.</summary>
        public static bool Bright(DamageType type) => type == DamageType.Fire || type == DamageType.Lightning || type == DamageType.Holy;

        protected override void OnStung(PlayerController h, float dealt)
        {
            if (dealt > 0f) health.Heal(dealt * drink);
        }

        protected override void OnDamaged(DamageInfo d, float amount)
        {
            base.OnDamaged(d, amount);
            if (!GameSession.IsAuthority || IsDead || d.dot || !Bright(d.type)) return;
            Vector2 from = d.source != null ? (Vector2)d.source.transform.position : d.point;
            Scatter(from, scatterSeconds);
            NetCues.Sound(wingSound, 0.7f, 0.2f, transform.position, 0.2f);
        }
    }
}
