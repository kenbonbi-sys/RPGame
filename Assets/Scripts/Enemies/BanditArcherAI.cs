using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Cung Thủ Hắc Phong (Thảo Nguyên Gió): a bandit archer who keeps a hero at bow's length. It
    /// walks back out of reach, and a hero who closes in anyway sees it spring away (Nhảy Lùi).
    /// Mũi Tên Đón Gió: it draws (a line shows where the arrow flies), reading the wind, and looses
    /// an arrow aimed into it so the gust carries it onto the mark; Mưa Tên: three arrows in a fan,
    /// shot straight, which the wind bends as it will. Thủ Lĩnh Hắc Phong calls these to his hill
    /// and sends them away for his duel (<see cref="EnemyBase.Retire"/>).
    /// </summary>
    public class BanditArcherAI : EnemyBase
    {
        [Header("Archer")]
        public float keepMin = 4.5f, keepMax = 8.5f;
        public float aimSeconds = 0.75f, arrowSpeed = 13f, arrowDamage = 28f;
        public float volleySeconds = 0.9f, volleySpread = 22f, volleyDamage = 20f, volleyCooldown = 6f;
        public int volleyCount = 3;
        public float leapDistance = 3.4f, leapSeconds = 0.3f, leapCooldown = 3.5f;

        enum Step { Aim, Volley, Leap }
        Step step;
        bool shot;
        Vector2 aim, mark;
        float nextVolley, nextLeap, strafeSign = 1f, nextStrafeFlip;

        protected override void OnEnable()
        {
            base.OnEnable();
            nextVolley = Time.time + 3f;
            nextLeap = 0f;
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            Vector2 at = p.transform.position;
            Vector2 away = Pos - at;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle;
            // too close: spring back out of reach, if the ground behind is open
            if (dist < keepMin - 1.5f && Time.time >= nextLeap && !Util.LineBlocked(Pos, Pos + away.normalized * leapDistance))
            {
                Begin(p, Step.Leap);
                return;
            }
            bool clear = !Util.LineBlocked(Pos, at);
            if (dist <= keepMax && dist >= keepMin - 1.5f && clear && Time.time >= nextAttack)
            {
                Begin(p, Time.time >= nextVolley ? Step.Volley : Step.Aim);
                return;
            }
            if (dist > keepMax || !clear)
            {
                MoveTo(at, keepMax - 1f);
                return;
            }
            // in bow's length: back off from a hero coming in, else step sideways around them
            if (Time.time >= nextStrafeFlip)
            {
                nextStrafeFlip = Time.time + Random.Range(1.2f, 2.4f);
                strafeSign = Random.value < 0.5f ? -1f : 1f;
            }
            Vector2 dir = dist < keepMin ? away.normalized : Vector2.Perpendicular(away.normalized) * strafeSign;
            if (Util.LineBlocked(Pos, Pos + dir * 1.2f)) dir = -dir;
            motor.Move(dir, 0.7f * (status != null ? status.SpeedMultiplier : 1f));
            PlayMove();
            Face(at);
        }

        void Begin(PlayerController p, Step s)
        {
            SetState(State.Attack);
            step = s;
            shot = false;
            motor.Stop();
            Vector2 at = p.transform.position;
            Face(at);
            if (s == Step.Leap)
            {
                Vector2 away = Pos - at;
                aim = away.sqrMagnitude > 0.01f ? away.normalized : Vector2.left;
                if (anim != null) anim.Play("dash", true);
                motor.Dash(aim * (leapDistance / leapSeconds), leapSeconds);
                NetCues.Vfx("dash_burst", Pos, 0f, 0.8f);
                NetCues.Sound("sfx_dash", 0.6f, 0.1f, transform.position);
                nextLeap = Time.time + leapCooldown;
                return;
            }
            if (anim != null) anim.Play("aim", true);
            Vector2 from = Pos + Vector2.up * 0.7f;
            float speed = arrowSpeed;
            if (s == Step.Aim)
            {
                // reads the wind: the line shows where the arrow will fly, not where the bow points
                mark = at + Vector2.up * 0.5f;
                aim = EnemyShots.IntoTheWind(from, mark, speed);
                Vector2 path = (at + Vector2.up * 0.5f - from);
                EnemyShots.WarnLine(this, from, path, Mathf.Min(path.magnitude + 1.5f, speed * 1.4f), 0.2f, 0.8f, aimSeconds, t => Warn(t));
                NetCues.Announce(health, "Kỹ năng: Mũi Tên Đón Gió");
            }
            else
            {
                aim = (at + Vector2.up * 0.5f - from).normalized;
                for (int i = 0; i < volleyCount; i++)
                {
                    float a = (i - (volleyCount - 1) * 0.5f) * volleySpread;
                    Vector2 d = Quaternion.Euler(0, 0, a) * aim;
                    EnemyShots.WarnLine(this, from, d, keepMax + 1f, 0.18f, 0.9f, volleySeconds, t => Warn(t));
                }
                NetCues.Announce(health, "Kỹ năng: Mưa Tên");
            }
            NetCues.Sound("sfx_bow_draw", 0.7f, 0.08f, transform.position);
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            if (step == Step.Leap)
            {
                if (stateTime < leapSeconds + 0.15f) return;
                SetState(State.Chase);
                return;
            }
            motor.Stop();
            float wait = step == Step.Aim ? aimSeconds : volleySeconds;
            if (!shot && stateTime >= wait)
            {
                shot = true;
                ForgetWarnings();
                if (anim != null) anim.Play("attack", true);
                Vector2 from = Pos + Vector2.up * 0.7f;
                NetCues.Sound("sfx_bow", 0.8f, 0.1f, transform.position);
                // aimed again as it looses (the gust may have shifted it): onto the mark it drew on
                if (step == Step.Aim) EnemyShots.Arrow(gameObject, from, EnemyShots.IntoTheWind(from, mark, arrowSpeed), arrowDamage, arrowSpeed, "Mũi Tên Đón Gió");
                else
                    for (int i = 0; i < volleyCount; i++)
                    {
                        float a = (i - (volleyCount - 1) * 0.5f) * volleySpread;
                        EnemyShots.Arrow(gameObject, from, Quaternion.Euler(0, 0, a) * aim, volleyDamage, arrowSpeed * 0.9f, "Mưa Tên");
                    }
            }
            if (stateTime < wait + 0.35f) return;
            if (step == Step.Volley) nextVolley = Time.time + volleyCooldown / AttackSpeed;
            nextAttack = Time.time + attackCooldown * Random.Range(0.85f, 1.2f) / AttackSpeed;
            SetState(State.Chase);
        }

        /// <summary>Draws and shoots at <paramref name="p"/> now (AutoShot, tests); a volley when <paramref name="volley"/>. Where the rules run.</summary>
        public void DebugShoot(PlayerController p, bool volley = false)
        {
            if (!GameSession.IsAuthority || p == null || IsDead) return;
            target = p;
            threat.Add(p, 1f);
            Begin(p, volley ? Step.Volley : Step.Aim);
        }

        /// <summary>Springs back from <paramref name="p"/> now (AutoShot, tests). Where the rules run.</summary>
        public void DebugLeap(PlayerController p)
        {
            if (!GameSession.IsAuthority || p == null || IsDead) return;
            target = p;
            threat.Add(p, 1f);
            Begin(p, Step.Leap);
        }

        protected override void OnAttackInterrupted()
        {
            if (step == Step.Leap || shot) return;
            SetState(State.Chase);
        }
    }
}
