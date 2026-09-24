using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Gấu Ma Rừng Già — the forest boss (the shared boss rules: <see cref="BossBase"/>).
    /// Patterns: Vồ (claw swipe), Dậm Đất (ground stomp, stuns), Ném Đá Lớn (throws a boulder
    /// that stays on the field), Chụp Quăng (leap slam; landing on a boulder stuns the bear).
    /// Enraged: a second swipe, a second wider stomp, three rocks.
    /// </summary>
    public class BossBear : BossBase
    {
        [Header("Tuning")]
        public float swipeRange = 2.7f, swipeDamage = 18f;
        public float stompRadius = 4.2f, stompDamage = 26f, stompStun = 1.2f;
        public float rockRadius = 1.6f, rockDamage = 22f, rockFlight = 1.05f;
        public float pounceRadius = 2.4f, pounceDamage = 30f, pounceAir = 0.75f;

        static readonly string[] AttackIds = { "swipe", "stomp", "rock", "pounce" };
        public override string[] Attacks => AttackIds;

        protected override void OnFightStart()
        {
            SetCooldown("stomp", 2.5f);
            SetCooldown("pounce", 4f);
        }

        protected override bool Decide(PlayerController p, float dist)
        {
            string pick = PickWeighted(
                ("swipe", 5f, dist < swipeRange),
                ("stomp", 3f, dist < stompRadius * 0.85f),
                ("rock", 3f, dist > 3.5f),
                ("pounce", 2.5f, dist > 2.5f && dist < 9f));
            if (pick == null) return false;
            Run(AttackRoutine(pick, p));
            return true;
        }

        protected override IEnumerator AttackRoutine(string id, PlayerController p)
        {
            switch (id)
            {
                case "swipe": return Swipe(p);
                case "stomp": return Stomp();
                case "rock": return RockThrow(p);
                case "pounce": return Pounce(p);
                default: return null;
            }
        }

        IEnumerator Swipe(PlayerController p)
        {
            SetCooldown("swipe", 1.4f);
            Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
            Face(p.transform.position);
            Announce("Vồ");
            float windup = enraged ? 0.42f : 0.55f;
            Warn(NetCues.Cone(this, Pos + Vector2.up * 0.3f, dir, swipeRange + 0.3f, windup));
            anim.Play("windup", true);
            NetCues.Sound("sfx_telegraph", 0.5f, 0.05f, transform.position);
            yield return new WaitForSeconds(windup);
            anim.Play("slam", true);
            motor.Dash(dir * 5f, 0.12f);
            NetCues.Sound("sfx_boss_swipe", 1f, 0.08f, transform.position);
            NetCues.Vfx("claw_swipe", Pos + Vector2.up * 0.9f + dir * 1.4f, Util.Angle(dir) - 90f, 1.4f);
            var d = DamageInfo.Make(swipeDamage, Team.Enemy, gameObject, Pos, dir, DamageType.Physical, 7f);
            d.skillName = "Vồ";
            Combat.DamageCone(Pos + Vector2.up * 0.3f, dir, swipeRange + 0.3f, 100f, d);
            NetCues.Shake(0.2f, Pos);
            yield return new WaitForSeconds(0.45f);
            if (enraged && Random.value < 0.5f)
            {
                // quick second swipe, at whoever it is angriest with now
                var next = PickTarget();
                if (next != null) yield return Swipe(next);
            }
        }

        IEnumerator Stomp()
        {
            SetCooldown("stomp", 6f);
            int count = enraged ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                Announce("Dậm Đất");
                float windup = i == 0 ? 1.0f : 0.7f;
                float radius = stompRadius + i * 1.2f;
                Warn(NetCues.Circle(this, Pos, radius, windup));
                anim.Play("windup", true);
                NetCues.Sound("sfx_telegraph", 0.6f, 0.05f, transform.position);
                yield return new WaitForSeconds(windup);
                anim.Play("slam", true);
                NetCues.Sound("sfx_boss_stomp", 1f, 0.05f, transform.position);
                NetCues.Vfx("stomp_shockwave", Pos + Vector2.up * 0.1f, 0f, radius / 4.2f);
                NetCues.Shake(0.65f, Pos);
                NetCues.Impact(0.6f, 0.4f, Pos);
                if (GameSession.HasScreen) TimeFX.HitStop(0.06f, gameObject);
                var d = DamageInfo.Make(stompDamage, Team.Enemy, gameObject, Pos, Vector2.down, DamageType.Physical, 8f);
                d.status.stun = stompStun;
                d.skillName = "Dậm Đất";
                Combat.DamageCircle(Pos, radius, d);
                yield return new WaitForSeconds(0.55f);
            }
        }

        IEnumerator RockThrow(PlayerController p)
        {
            SetCooldown("rock", 5f);
            Announce("Ném Đá Lớn");
            Face(p.transform.position);
            anim.Play("throw", true);
            anim.speed = 0; // hold the "rock overhead" frame
            int rocks = enraged ? 3 : 1;
            var targets = new List<Vector2>();
            for (int i = 0; i < rocks; i++)
            {
                Vector2 t = (Vector2)p.transform.position + p.motor.Velocity * 0.5f;
                if (i > 0) t += Util.RandomInCircle(3.2f);
                if (arenaCenter != null && Vector2.Distance(t, home) > arenaRadius) t = home + (t - home).normalized * arenaRadius;
                targets.Add(t);
                Warn(NetCues.Circle(this, t, rockRadius, 0.75f + rockFlight + i * 0.18f));
            }
            NetCues.Sound("sfx_telegraph", 0.5f, 0.05f, transform.position);
            yield return new WaitForSeconds(0.75f);
            anim.speed = 1;
            anim.Play("throw", true);
            for (int i = 0; i < rocks; i++)
            {
                LaunchRock(targets[i]);
                yield return new WaitForSeconds(0.18f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        protected override void OnInterrupted()
        {
            if (anim != null) anim.speed = 1f;   // a stun in the middle of a throw
        }

        void LaunchRock(Vector2 target)
        {
            var db = GameManager.I.db;
            if (db.rockProjectilePrefab == null) return;
            Vector2 start = Pos + new Vector2(body != null && body.flipX ? -1f : 1f, 3f);
            var go = Pool.Get(db.rockProjectilePrefab, start, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            NetCues.Sound("sfx_rock_throw", 0.9f, 0.08f, transform.position);
            NetCues.Arc(start, target, rockFlight);
            go.GetComponent<ArcProjectile>().Launch(start, target, rockFlight, RockLanded);
        }

        void RockLanded(Vector2 at)
        {
            NetCues.Vfx("rock_impact", at);
            NetCues.Sound("sfx_rock_impact", 1f, 0.06f, at);
            NetCues.Shake(0.35f, at);
            var d = DamageInfo.Make(rockDamage, Team.Enemy, gameObject, at, Vector2.down, DamageType.Physical, 6f);
            d.skillName = "Ném Đá Lớn";
            Combat.DamageCircle(at, rockRadius, d);
            // the rock stays on the field as a "Tảng Đá Lớn"
            var db = GameManager.I.db;
            bool free = Boulder.Nearest(at, 1.6f) == null && Boulder.All.Count < 6 &&
                        Physics2D.OverlapCircle(at, 0.5f, Layers.ObstacleMask) == null;
            if (db.boulderPrefab != null && free)
            {
                var rock = Instantiate(db.boulderPrefab, at, Quaternion.identity, transform.parent);
                NetWorld.BoulderMade(rock.GetComponent<Boulder>());
            }
        }

        IEnumerator Pounce(PlayerController p)
        {
            SetCooldown("pounce", 6.5f);
            Announce("Chụp Quăng");
            Face(p.transform.position);
            anim.Play("crouch", true);
            Vector2 target = p.transform.position;
            if (Vector2.Distance(target, home) > arenaRadius) target = home + (target - home).normalized * arenaRadius;
            float crouch = enraged ? 0.35f : 0.5f;
            Warn(NetCues.Circle(this, target, pounceRadius, crouch + pounceAir));
            NetCues.Sound("sfx_telegraph", 0.6f, 0.05f, transform.position);
            yield return new WaitForSeconds(crouch);

            anim.Play("air", true);
            NetCues.Sound("sfx_boss_leap", 1f, 0.05f, transform.position);
            NetCues.Vfx("step_dust", Pos, 0f, 3f);
            if (afterImages != null && GameSession.HasScreen) afterImages.Emit(pounceAir, new Color(0.6f, 0.4f, 1f, 0.5f));
            var cols = GetComponentsInChildren<Collider2D>();
            foreach (var c in cols) c.enabled = false;
            Vector2 start = Pos;
            float t = 0;
            while (t < pounceAir)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / pounceAir);
                motor.Teleport(Vector2.Lerp(start, target, Util.EaseInOut(k)));
                if (bodyRoot != null) bodyRoot.localPosition = new Vector3(0, 4f * 3f * k * (1 - k), 0);
                yield return null;
            }
            if (bodyRoot != null) bodyRoot.localPosition = Vector3.zero;
            foreach (var c in cols) c.enabled = true;

            anim.Play("slam", true);
            NetCues.Vfx("pounce_land", target);
            NetCues.Sound("sfx_boss_stomp", 0.8f, 0.1f, target);
            NetCues.Shake(0.55f, target);
            NetCues.Impact(0.5f, 0.35f, target);
            var d = DamageInfo.Make(pounceDamage, Team.Enemy, gameObject, target, Vector2.down, DamageType.Physical, 9f);
            d.skillName = "Chụp Quăng";
            Combat.DamageCircle(target, pounceRadius, d);

            // landing on a boulder shatters it and dazes the bear
            var rock = Boulder.Nearest(target, 1.9f);
            if (rock != null)
            {
                rock.Shatter(false);
                status.ForceStun(2.8f);
                NetCues.Log($"{displayName} đâm sầm vào Tảng Đá Lớn và bị choáng!", Palette.Status, target);
                EnterStun();
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
}
