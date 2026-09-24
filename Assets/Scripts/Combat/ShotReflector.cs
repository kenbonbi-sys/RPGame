using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Crystal that turns a hero's shot back (Slime Pha Lê all around, Golem Pha Lê Cổ only on
    /// its face): the shot bursts on it without hurting it and a splinter of crystal flies back at
    /// whoever fired it (<see cref="EnemyShots.Shard"/>). Blades, blasts on the ground and beams
    /// are not shots: they hurt it as usual. Where the world's rules run.
    /// </summary>
    public class ShotReflector : MonoBehaviour
    {
        [Tooltip("Only shots coming at the side it faces (its body's flipX: looking left).")]
        public bool frontOnly;
        public SpriteRenderer body;
        public float damage = 20f;
        public float speed = 10f;
        public string skillName = "Phản Xạ Pha Lê";

        Health health;
        float nextText;

        void Awake() => health = GetComponent<Health>();

        /// <summary>Which way it looks (+1 right, −1 left).</summary>
        public float Facing => body != null && body.flipX ? -1f : 1f;

        /// <summary>Whether something coming from <paramref name="from"/> meets its face.</summary>
        public bool Faces(Vector2 from) => Armour.Side(transform.position, Facing, from) > 0f;

        /// <summary>
        /// <paramref name="shot"/> (a hero's) meets it at <paramref name="at"/>: true when it is
        /// turned back (the shot then deals no damage).
        /// </summary>
        public bool TryReflect(Projectile shot, Vector2 at)
        {
            if (!enabled || !GameSession.IsAuthority || shot == null) return false;
            if (health != null && (health.IsDead || health.invulnerable)) return false;
            Vector2 came = at - shot.Direction * 2f;   // where it came from
            if (frontOnly && !Faces(came)) return false;
            Vector2 back = shot.owner != null ? (Vector2)shot.owner.transform.position + Vector2.up * 0.4f - at : -shot.Direction;
            if (back.sqrMagnitude < 0.01f) back = -shot.Direction;
            EnemyShots.Shard(gameObject, at + back.normalized * 0.4f, at + back.normalized * 8f, damage, speed, skillName);
            NetCues.Vfx("crystal_hit", at, 0f, 1.2f);
            NetCues.Sound("sfx_reflect", 0.8f, 0.1f, at, 0.05f);
            if (Time.time >= nextText)
            {
                nextText = Time.time + 0.8f;
                NetCues.WorldText("Phản xạ!", (Vector3)at + Vector3.up * 0.8f, new Color(0.6f, 0.95f, 1f));
            }
            return true;
        }
    }

    /// <summary>Where a blow lands on a creature that has a front and a back.</summary>
    public static class Armour
    {
        /// <summary>
        /// +1: straight at its face, −1: straight at its back, 0: from above or below; for a
        /// creature at <paramref name="self"/> looking along <paramref name="facing"/> (±1 on x).
        /// </summary>
        public static float Side(Vector2 self, float facing, Vector2 from)
        {
            Vector2 to = from - self;
            if (to.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Clamp(to.normalized.x * Mathf.Sign(facing), -1f, 1f);
        }

        /// <summary>
        /// Where a hit came from: its attacker when it has one nearby (a blade, a hero's blast),
        /// otherwise back along its direction (a shot, a burst on the ground).
        /// </summary>
        public static Vector2 Origin(DamageInfo d, Vector2 self)
        {
            if (d.source != null)
            {
                Vector2 src = d.source.transform.position;
                if ((src - self).sqrMagnitude < 4.5f * 4.5f) return src;
            }
            if (d.direction.sqrMagnitude > 0.0001f) return self - d.direction.normalized * 2f;
            return d.point;
        }
    }
}
