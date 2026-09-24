using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Người Bùn (Đầm Lầy Sương Mù): slow and tough. Raises both arms and slams the ground in
    /// front of it (the mud slows whoever it hits); when it falls it splits into Bùn Con, its
    /// <see cref="brood"/>.
    /// </summary>
    public class MudManAI : EnemyBase
    {
        [Header("Mud man")]
        public float slamReach = 1.1f;
        public float slamRadius = 1.55f;
        public float slamWindup = 0.75f;
        public float slamDamage = 26f;
        [Range(0f, 1f)] public float slamSlow = 0.4f;
        [Tooltip("Where its Bùn Con wait (placed with the camp).")]
        public Brood brood;
        public int splitInto = 2;

        bool slammed;
        Vector2 slamAt;

        protected override void Think()
        {
            // an attack cut short (a stagger, a stun) must not leave the animation slowed
            if (state != State.Attack && anim != null && anim.speed != 1f) anim.speed = 1f;
            base.Think();
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            if (dist <= slamReach + slamRadius * 0.8f && Time.time >= nextAttack)
            {
                SetState(State.Attack);
                slammed = false;
                motor.Stop();
                Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
                slamAt = Pos + dir * slamReach + Vector2.up * 0.1f;
                Face(p.transform.position);
                NetCues.Circle(this, slamAt, slamRadius, slamWindup);
                NetCues.Sound("sfx_telegraph", 0.45f, 0.05f, transform.position);
                if (anim != null)
                {
                    // arms up through the windup, the slam frame lands with the hit
                    anim.Play("attack", true);
                    anim.speed = 2f / Mathf.Max(0.2f, slamWindup) / 8f;
                }
                return;
            }
            MoveTo(p.transform.position, slamReach);
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            motor.Stop();
            if (!slammed && stateTime >= slamWindup)
            {
                slammed = true;
                if (anim != null) anim.speed = 1f;
                NetCues.Vfx("mud_splat", slamAt, 0f, 1.2f);
                NetCues.Sound("sfx_mud_slam", 0.9f, 0.08f, slamAt);
                NetCues.Shake(0.2f, slamAt);
                var d = DamageInfo.Make(slamDamage, Team.Enemy, gameObject, slamAt, Vector2.down, DamageType.Physical, 6f);
                d.status.slow = slamSlow;
                d.status.slowDuration = 2f;
                d.skillName = "Đập Bùn";
                Combat.DamageCircle(slamAt, slamRadius, d);
            }
            if (stateTime > slamWindup + 0.6f)
            {
                nextAttack = Time.time + attackCooldown * Random.Range(0.9f, 1.2f) / AttackSpeed;
                SetState(State.Chase);
            }
        }

        protected override void OnDied(DamageInfo d)
        {
            if (anim != null) anim.speed = 1f;
            base.OnDied(d);
            if (GameSession.IsAuthority && brood != null)
            {
                int n = brood.Release(transform.position, splitInto, d.SourcePlayer, 0.7f);
                if (n > 0) NetCues.Log($"{displayName} tách thành {n} Bùn Con!", Palette.LogInfo, transform.position, NetCues.NearRadius);
            }
            if (!GameSession.HasScreen) return;
            AudioManager.Play("sfx_mud_slam", 0.6f, 0.1f, transform.position);
            VFX.Spawn("mud_splat", transform.position + Vector3.up * 0.2f, Quaternion.identity, 1.4f);
        }
    }
}
