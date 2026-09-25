using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Bò Rừng Sắt — the mini-boss of Bãi Sừng Sắt (the shared boss rules: <see cref="BossBase"/>).
    /// A steppe bison the Hắc Phong bandits clad in riveted iron and set to guard the way to their
    /// camp. The plates on its head and shoulders turn blows aside ("Giáp sắt!"); its flank and
    /// rump take them in full and more. Lao Húc: it paws the ground (a line shows its run) and
    /// charges in a straight line through anyone in it; lured into one of the sandstone blocks
    /// ringing its ground, it rams it, reels for <see cref="dazeSeconds"/> and its plates spring
    /// loose ("Giáp bung!": every blow bites deep). Dậm Đất: a stamp that knocks everyone around it
    /// off their feet; Hất Sừng: a toss of its horns in front. Enraged it charges three times in a
    /// row (Húc Liên Hoàn), turning on its prey between runs.
    /// </summary>
    public class BossIronBison : BossBase
    {
        [Header("Iron and hide")]
        [Range(0f, 1f)] public float frontGuard = 0.5f;
        public float flankBonus = 1.25f;
        public float dazedBonus = 1.6f;

        [Header("Tuning")]
        public float chargeWindup = 0.9f, rampageWindup = 0.55f, chargeSpeed = 13f, chargeLength = 13f, chargeDamage = 55f;
        public float dazeSeconds = 3.2f;
        public float stompWindup = 0.7f, stompRadius = 3.2f, stompDamage = 40f, stompStun = 0.6f;
        public float tossWindup = 0.5f, tossRadius = 2.5f, tossDamage = 45f;

        static readonly string[] AttackIds = { "charge", "stomp", "toss", "rampage" };
        public override string[] Attacks => AttackIds;

        float dazedUntil, nextText;
        Collider2D[] own;
        readonly RaycastHit2D[] cast = new RaycastHit2D[8];
        readonly HashSet<PlayerController> rammed = new HashSet<PlayerController>();
        readonly List<PlayerController> near = new List<PlayerController>();

        /// <summary>Reeling after it rammed rock: its plates hang loose.</summary>
        public bool Dazed => Time.time < dazedUntil;

        /// <summary>Which way it looks (+1 right, −1 left).</summary>
        float Facing => body != null && body.flipX ? -1f : 1f;

        protected override void Awake()
        {
            base.Awake();
            own = GetComponentsInChildren<Collider2D>(true);
            health.Guard = Guard;
        }

        float Guard(DamageInfo d)
        {
            if (Dazed)
            {
                Text("Giáp bung!", Palette.Crit);
                return dazedBonus;
            }
            float side = Armour.Side(Pos, Facing, Armour.Origin(d, Pos));
            if (side > 0.3f)
            {
                if (Time.time >= nextText) NetCues.Sound("sfx_iron", 0.5f, 0.15f, transform.position, 0.3f);
                Text("Giáp sắt!", new Color(0.75f, 0.8f, 0.9f));
                NetCues.Vfx("hit_spark", d.point, 0f, 0.9f);
                return frontGuard;
            }
            if (side < -0.2f) return flankBonus;
            return 1f;
        }

        void Text(string s, Color c)
        {
            if (Time.time < nextText) return;
            nextText = Time.time + 0.7f;
            NetCues.WorldText(s, health.HeadPosition + Vector3.up * 0.2f, c);
        }

        protected override void OnFightStart()
        {
            SetCooldown("charge", 1.5f);
            SetCooldown("stomp", 3f);
            SetCooldown("rampage", 2f);
        }

        protected override void OnFightReset()
        {
            dazedUntil = 0f;
            if (anim != null) anim.speed = 1f;
        }

        protected override void OnInterrupted()
        {
            if (anim != null) anim.speed = 1f;
        }

        protected override bool Decide(PlayerController p, float dist)
        {
            float side = Armour.Side(Pos, Facing, p.transform.position);
            string pick = PickWeighted(
                ("rampage", 8f, enraged && dist > 3f),
                ("charge", 6f, dist > 3.5f && dist < chargeLength),
                ("toss", 5f, dist < tossRadius + 0.4f && side > -0.2f),
                ("stomp", 4f, dist < stompRadius + 0.5f));
            if (pick == null) return false;
            Run(AttackRoutine(pick, p));
            return true;
        }

        protected override IEnumerator AttackRoutine(string id, PlayerController p)
        {
            switch (id)
            {
                case "charge": return Charge(p);
                case "stomp": return Stomp(p);
                case "toss": return Toss(p);
                case "rampage": return Rampage(p);
                default: return null;
            }
        }

        // ================================================================= Lao Húc
        IEnumerator Charge(PlayerController p)
        {
            SetCooldown("charge", 5.5f);
            Announce("Lao Húc");
            yield return Run1(p, chargeWindup);
        }

        IEnumerator Rampage(PlayerController p)
        {
            SetCooldown("rampage", 12f);
            SetCooldown("charge", 6f);
            Announce("Húc Liên Hoàn");
            for (int i = 0; i < 3; i++)
            {
                var prey = Target != null && !Target.IsDead ? Target : p;
                if (prey == null || prey.IsDead) yield break;
                yield return Run1(prey, i == 0 ? chargeWindup : rampageWindup);
                if (Dazed) yield break;   // it rammed rock: the rest of the rampage is lost
                yield return new WaitForSeconds(0.25f);
            }
            // winded after three runs
            anim.Play("idle", true);
            yield return new WaitForSeconds(1.2f);
        }

        /// <summary>One run: paws the ground behind a line, then charges along it.</summary>
        IEnumerator Run1(PlayerController p, float windup)
        {
            Vector2 to = (Vector2)p.transform.position - Pos;
            Vector2 aim = to.sqrMagnitude > 0.01f ? to.normalized : new Vector2(Facing, 0f);
            Face(p.transform.position);
            float length = Mathf.Min(chargeLength, Vector2.Distance(Pos, home) + arenaRadius + 2f);
            anim.Play("windup", true);
            NetCues.Sound("sfx_bison", 1f, 0.05f, transform.position);
            NetCues.Vfx("step_dust", Pos + aim * 0.8f, 0f, 1.4f);
            EnemyShots.WarnLine(this, Pos + Vector2.up * 0.25f, aim, length, 0.75f, 1.1f, windup, t => Warn(t));
            yield return new WaitForSeconds(windup);
            ClearTelegraphs();
            rammed.Clear();
            anim.Play("attack", true);
            anim.speed = 1.6f;
            NetCues.Sound("sfx_boss_leap", 0.8f, 0.1f, transform.position);
            float ran = 0f;
            while (ran < length)
            {
                float step = chargeSpeed * Time.deltaTime * status.SpeedMultiplier;
                if (Rammed(aim, step)) break;
                Vector2 next = Pos + aim * step;
                var zone = ZoneRoot.Current;
                if (Vector2.Distance(next, home) > LeashRadius || (zone != null && (zone.IsChasm(next + aim * 0.8f) || zone.IsWall(next + aim * 0.8f)))) break;
                motor.Teleport(next);
                ran += step;
                Gore(aim);
                if (Time.frameCount % 4 == 0) NetCues.Vfx("step_dust", Pos, 0f, 1f);
                yield return null;
            }
            anim.speed = 1f;
            motor.Stop();
            if (!Dazed)
            {
                anim.Play("idle", true);
                yield return new WaitForSeconds(0.35f);
            }
        }

        /// <summary>Rock just ahead: it rams it, reels and its plates spring loose (true: the run is over).</summary>
        bool Rammed(Vector2 aim, float step)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(Layers.ObstacleMask);
            filter.useTriggers = false;
            int n = Physics2D.CircleCast(Pos + Vector2.up * 0.35f, 0.7f, aim, filter, cast, step + 0.2f);
            for (int i = 0; i < n; i++)
            {
                var c = cast[i].collider;
                if (c == null || System.Array.IndexOf(own, c) >= 0 || c.GetComponentInParent<Health>() != null) continue;
                NetCues.Vfx("rock_impact", cast[i].point);
                NetCues.Vfx("rock_chips", cast[i].point, 0f, 1.4f);
                NetCues.Sound("sfx_rock_impact", 1f, 0.05f, cast[i].point);
                NetCues.Sound("sfx_iron", 0.9f, 0.05f, transform.position);
                NetCues.Shake(0.45f, Pos);
                NetCues.WorldText("Giáp bung!", health.HeadPosition + Vector3.up * 0.6f, Palette.Crit);
                dazedUntil = Time.time + dazeSeconds;
                status.ForceStun(dazeSeconds);
                return true;
            }
            return false;
        }

        /// <summary>Whoever it runs over is gored and thrown, once a run.</summary>
        void Gore(Vector2 aim)
        {
            foreach (var h in Players.All)
            {
                if (h == null || h.IsDead || rammed.Contains(h) || Vector2.Distance(h.transform.position, Pos) > 1.25f) continue;
                rammed.Add(h);
                var d = DamageInfo.Make(chargeDamage, Team.Enemy, gameObject, h.transform.position, aim, DamageType.Physical, 9f);
                d.skillName = "Lao Húc";
                d.status.stun = 0.35f;
                d.feedback = true;
                if (h.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(h.health, d);
            }
        }

        // ================================================================= Dậm Đất, Hất Sừng
        IEnumerator Stomp(PlayerController p)
        {
            SetCooldown("stomp", 7f);
            Announce("Dậm Đất");
            anim.Play("windup", true);
            Warn(NetCues.Circle(this, Pos, stompRadius, stompWindup));
            yield return new WaitForSeconds(stompWindup);
            anim.Play("attack", true);
            NetCues.Vfx("stomp_shockwave", Pos, 0f, stompRadius / 3f);
            NetCues.Sound("sfx_rock_impact", 1f, 0.05f, transform.position);
            NetCues.Shake(0.4f, Pos);
            var d = DamageInfo.Make(stompDamage, Team.Enemy, gameObject, Pos, Vector2.down, DamageType.Physical, 6f);
            d.skillName = "Dậm Đất";
            d.status.stun = stompStun;
            Combat.DamageCircle(Pos, stompRadius, d);
            yield return new WaitForSeconds(0.6f);
        }

        IEnumerator Toss(PlayerController p)
        {
            SetCooldown("toss", 3.5f);
            Announce("Hất Sừng");
            Vector2 to = (Vector2)p.transform.position - Pos;
            Vector2 aim = to.sqrMagnitude > 0.01f ? to.normalized : new Vector2(Facing, 0f);
            Face(p.transform.position);
            anim.Play("windup", true);
            Warn(NetCues.Cone(this, Pos, aim, tossRadius, tossWindup));
            yield return new WaitForSeconds(tossWindup);
            anim.Play("attack", true);
            NetCues.Sound("sfx_boss_swipe", 0.9f, 0.08f, transform.position);
            NetCues.Vfx("claw_swipe", Pos + aim * 1.2f + Vector2.up * 0.5f, Util.Angle(aim), 1.2f);
            var d = DamageInfo.Make(tossDamage, Team.Enemy, gameObject, Pos, aim, DamageType.Physical, 10f);
            d.skillName = "Hất Sừng";
            Combat.DamageCone(Pos + Vector2.up * 0.3f, aim, tossRadius, 110f, d);
            yield return new WaitForSeconds(0.5f);
        }

        protected override IEnumerator Enrage()
        {
            yield return base.Enrage();
            NetCues.Log("Giáp sắt của Bò Rừng Sắt đỏ rực. Nó sẽ húc liên hoàn!", new Color(1f, 0.6f, 0.4f), Pos);
        }
    }
}
