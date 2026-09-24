using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Golem Pha Lê Cổ — the mini-boss of Điện Pha Lê (the shared boss rules: <see cref="BossBase"/>).
    /// An old golem of crystal: its face is armour that turns shots back (<see cref="ShotReflector"/>,
    /// front only) and blunts blows, its glowing core sits in its back and takes them in full and
    /// more (<see cref="Health.Guard"/>). It turns slowly, so heroes can get behind it; a hero who
    /// stays there too long meets Xoay Lõi, a spin around itself. Nắm Đấm Pha Lê: both fists down in
    /// front; Gai Pha Lê: a row of crystal spikes erupting toward a hero; Mưa Mảnh: a fan of
    /// splinters. Enraged it turns faster and adds Tia Lõi, a beam from its eye that bounces off
    /// the hall's walls.
    /// </summary>
    public class BossCrystalGolem : BossBase
    {
        [Header("Crystal and core")]
        [Range(0f, 1f)] public float frontGuard = 0.55f;
        public float coreBonus = 1.7f;
        public float turnDelay = 1.3f;
        public float enragedTurnDelay = 0.8f;

        [Header("Tuning")]
        public float fistReach = 1.7f, fistRadius = 2f, fistDamage = 44f, fistStun = 0.8f;
        public int spikeCount = 7;
        public float spikeSpacing = 1.15f, spikeRadius = 0.85f, spikeDamage = 34f;
        public int shardCount = 5;
        public float shardSpread = 60f, shardDamage = 22f, shardSpeed = 9f;
        public float spinRadius = 3.3f, spinDamage = 40f;
        [Tooltip("Seconds a hero may stand at its back before it spins.")]
        public float behindPatience = 1.6f;
        public float beamRange = 13f, beamWidth = 0.6f, beamDamage = 48f;
        public Color beamColor = new Color(1f, 0.75f, 0.35f, 1f);

        static readonly string[] AttackIds = { "fists", "spikes", "shards", "spin", "beam" };
        public override string[] Attacks => AttackIds;

        float facing = 1f;
        float nextTurn;
        float behind;
        float nextText;
        // spikes still to erupt belong to a wave; a stun or a reset ends the wave (their warnings are gone)
        int wave;

        /// <summary>Which way it looks (+1 right, −1 left).</summary>
        public float Facing => facing;
        protected override bool TurnsFreely => false;
        Vector2 Eye => Pos + new Vector2(facing * 0.7f, 1.1f);

        protected override void Awake()
        {
            base.Awake();
            health.Guard = Guard;
        }

        protected override void Start()
        {
            base.Start();
            facing = body != null && body.flipX ? -1f : 1f;
        }

        float Guard(DamageInfo d)
        {
            float side = Armour.Side(Pos, facing, Armour.Origin(d, Pos));
            if (side < -0.3f)
            {
                if (Time.time >= nextText)
                {
                    nextText = Time.time + 0.6f;
                    NetCues.WorldText("Trúng lõi!", health.HeadPosition + Vector3.up * 0.2f, Palette.Crit);
                    NetCues.Vfx("crystal_hit", Pos + new Vector2(-facing * 0.7f, 1.4f), 0f, 1.2f);
                }
                return coreBonus;
            }
            if (side > 0.3f)
            {
                if (Time.time >= nextText)
                {
                    nextText = Time.time + 0.8f;
                    NetCues.WorldText("Giáp pha lê!", health.HeadPosition + Vector3.up * 0.2f, new Color(0.75f, 0.85f, 1f));
                }
                return frontGuard;
            }
            return 1f;
        }

        /// <summary>Turns toward <paramref name="at"/> when it may (slowly), or at once.</summary>
        void TurnToward(Vector2 at, bool now = false)
        {
            float want = at.x < Pos.x ? -1f : 1f;
            if (Mathf.Abs(at.x - Pos.x) < 0.2f || want == facing) return;
            if (!now && Time.time < nextTurn) return;
            facing = want;
            nextTurn = Time.time + (enraged ? enragedTurnDelay : turnDelay);
            if (body != null) body.flipX = facing < 0f;
        }

        protected override void OnFightStart()
        {
            SetCooldown("spikes", 3f);
            SetCooldown("shards", 4.5f);
            SetCooldown("beam", 2f);
            behind = 0f;
        }

        protected override void OnFightReset()
        {
            behind = 0f;
            wave++;
            if (anim != null) anim.speed = 1f;
        }

        protected override void OnInterrupted()
        {
            wave++;
            if (anim != null) anim.speed = 1f;
        }

        protected override void Approach(PlayerController p)
        {
            TurnToward(p.transform.position);
            base.Approach(p);
        }

        protected override bool Decide(PlayerController p, float dist)
        {
            // a hero at its back: patience runs out
            float side = Armour.Side(Pos, facing, p.transform.position);
            behind = side < -0.3f && dist < spinRadius + 1f ? behind + Time.deltaTime : Mathf.Max(0f, behind - Time.deltaTime * 2f);
            string pick = PickWeighted(
                ("spin", 10f, behind >= behindPatience),
                ("fists", 5f, dist < fistReach + fistRadius && side > -0.3f),
                ("spikes", 4f, dist > 2.5f && dist < spikeCount * spikeSpacing + 1f),
                ("shards", 3f, dist > 3f),
                ("beam", 5f, enraged && dist > 3f));
            if (pick == null) return false;
            Run(AttackRoutine(pick, p));
            return true;
        }

        protected override IEnumerator AttackRoutine(string id, PlayerController p)
        {
            switch (id)
            {
                case "fists": return Fists(p);
                case "spikes": return Spikes(p);
                case "shards": return Shards(p);
                case "spin": return Spin(p);
                case "beam": return Beam(p);
                default: return null;
            }
        }

        IEnumerator Fists(PlayerController p)
        {
            SetCooldown("fists", 2.2f);
            TurnToward(p.transform.position);
            Announce("Nắm Đấm Pha Lê");
            Vector2 at = Pos + new Vector2(facing * fistReach, 0.1f);
            float windup = enraged ? 0.7f : 0.9f;
            Warn(NetCues.Circle(this, at, fistRadius, windup));
            anim.Play("windup", true);
            NetCues.Sound("sfx_telegraph", 0.6f, 0.05f, transform.position);
            yield return new WaitForSeconds(windup);
            anim.Play("slam", true);
            NetCues.Vfx("rock_impact", at, 0f, 1.6f);
            NetCues.Vfx("crystal_burst", at, 0f, 1.2f);
            NetCues.Sound("sfx_boss_stomp", 1f, 0.06f, at);
            NetCues.Shake(0.45f, at);
            var d = DamageInfo.Make(fistDamage, Team.Enemy, gameObject, at, Vector2.down, DamageType.Physical, 7f);
            d.status.stun = fistStun;
            d.skillName = "Nắm Đấm Pha Lê";
            Combat.DamageCircle(at, fistRadius, d);
            yield return new WaitForSeconds(0.7f);
        }

        IEnumerator Spikes(PlayerController p)
        {
            SetCooldown("spikes", 7f);
            TurnToward(p.transform.position, true);
            Announce("Gai Pha Lê");
            Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
            anim.Play("slam", true);
            anim.speed = 0f;   // fists on the ground while the spikes run
            NetCues.Sound("sfx_boss_stomp", 0.8f, 0.06f, transform.position);
            NetCues.Shake(0.3f, Pos);
            const float warn = 0.7f;
            for (int i = 0; i < spikeCount; i++)
            {
                Vector2 at = Pos + dir * (1.6f + i * spikeSpacing);
                if (Util.LineBlocked(Pos, at)) break;
                Warn(NetCues.Circle(this, at, spikeRadius, warn, new Color(0.6f, 0.9f, 1f, 0.8f)));
                StartCoroutine(Spike(at, warn, wave));
                yield return new WaitForSeconds(enraged ? 0.08f : 0.12f);
            }
            anim.speed = 1f;
            yield return new WaitForSeconds(warn + 0.3f);
        }

        IEnumerator Spike(Vector2 at, float after, int ofWave)
        {
            yield return new WaitForSeconds(after);
            if (state == State.Dead || ofWave != wave) yield break;
            NetCues.Vfx("ice_spike", at, 0f, 1.1f);
            NetCues.Vfx("crystal_burst", at + Vector2.up * 0.4f, 0f, 0.7f);
            NetCues.Sound("sfx_crystal", 0.6f, 0.15f, at, 0.05f);
            var d = DamageInfo.Make(spikeDamage, Team.Enemy, gameObject, at, Vector2.up, DamageType.Physical, 5f);
            d.status.stun = 0.3f;
            d.skillName = "Gai Pha Lê";
            Combat.DamageCircle(at, spikeRadius, d);
        }

        IEnumerator Shards(PlayerController p)
        {
            SetCooldown("shards", 6f);
            TurnToward(p.transform.position, true);
            Announce("Mưa Mảnh");
            anim.Play("cast", true);
            NetCues.Sound("sfx_crystal", 0.8f, 0.05f, transform.position);
            Vector2 mouth = Pos + new Vector2(facing * 0.6f, 1.6f);
            Vector2 dir = ((Vector2)p.transform.position + Vector2.up * 0.3f - mouth).normalized;
            for (int i = 0; i < shardCount; i++)
            {
                float a = shardCount > 1 ? -shardSpread * 0.5f + shardSpread * i / (shardCount - 1) : 0f;
                Warn(NetCues.Cone(this, mouth, Util.Rotate(dir, a), 1.2f, 0.55f, new Color(0.6f, 0.9f, 1f, 0.7f)));
            }
            yield return new WaitForSeconds(enraged ? 0.45f : 0.6f);
            for (int i = 0; i < shardCount; i++)
            {
                float a = shardCount > 1 ? -shardSpread * 0.5f + shardSpread * i / (shardCount - 1) : 0f;
                Vector2 d = Util.Rotate(dir, a);
                EnemyShots.Shard(gameObject, mouth + d * 0.4f, mouth + d * 10f, shardDamage, shardSpeed, "Mưa Mảnh");
            }
            yield return new WaitForSeconds(0.6f);
        }

        IEnumerator Spin(PlayerController p)
        {
            SetCooldown("spin", 5f);
            behind = 0f;
            Announce("Xoay Lõi");
            float windup = enraged ? 0.75f : 0.95f;
            Warn(NetCues.Circle(this, Pos, spinRadius, windup, new Color(1f, 0.7f, 0.35f, 0.85f)));
            anim.Play("windup", true);
            NetCues.Sound("sfx_telegraph", 0.7f, 0.05f, transform.position);
            yield return new WaitForSeconds(windup);
            anim.Play("spin", true);
            NetCues.Sound("sfx_bladestorm", 0.9f, 0.05f, transform.position);
            NetCues.Vfx("stomp_shockwave", Pos, 0f, 1.1f);
            NetCues.Vfx("crystal_burst", Pos + Vector2.up * 1f, 0f, 1.4f);
            NetCues.Shake(0.35f, Pos);
            var d = DamageInfo.Make(spinDamage, Team.Enemy, gameObject, Pos, Vector2.zero, DamageType.Physical, 9f);
            d.skillName = "Xoay Lõi";
            Combat.DamageCircle(Pos, spinRadius, d);
            // it comes out of the spin facing whoever it was after
            var hero = PickTarget() ?? p;
            if (hero != null) TurnToward(hero.transform.position, true);
            yield return new WaitForSeconds(0.7f);
        }

        IEnumerator Beam(PlayerController p)
        {
            SetCooldown("beam", 8f);
            TurnToward(p.transform.position, true);
            Announce("Tia Lõi");
            anim.Play("cast", true);
            anim.speed = 0f;
            Vector2 to = (Vector2)p.transform.position + Vector2.up * 0.35f - Eye;
            var path = EnemyShots.Trace(Eye, to, beamRange, true, gameObject);
            EnemyShots.WarnBeam(this, path, beamWidth * 0.8f, 1f, t => Warn(t));
            NetCues.Sound("sfx_lightning_charge", 0.8f, 0.05f, transform.position);
            yield return new WaitForSeconds(1f);
            anim.speed = 1f;
            NetCues.Sound("sfx_beam", 1f, 0.05f, transform.position);
            NetCues.Shake(0.3f, Pos);
            var d = DamageInfo.Make(beamDamage, Team.Enemy, gameObject, path.a, path.b - path.a, DamageType.Lightning, 4f);
            d.skillName = "Tia Lõi";
            EnemyShots.Beam(path, beamWidth, 0.6f, beamColor, d);
            yield return new WaitForSeconds(0.7f);
        }
    }
}
