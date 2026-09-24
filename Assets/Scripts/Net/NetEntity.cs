using UnityEngine;

namespace RPG
{
    public enum NetEntityKind : byte
    {
        Enemy = 1,
        Boss = 2,
        Boulder = 3,
        Loot = 4,
        Chest = 5
    }

    /// <summary>
    /// An object of the zone that the server runs and the players' screens copy (online phase 3):
    /// an enemy, the boss, a Tảng Đá Lớn, a loot drop. Added when an online session starts
    /// (offline nothing has it). On the server it reports its state for the snapshots; on a
    /// client it applies them: position (smoothed a little behind the server), animation,
    /// facing, health, statuses, and appears, falls and returns when the server says so.
    /// </summary>
    public class NetEntity : MonoBehaviour
    {
        /// <summary>Same on every machine: zone objects are numbered in scene order, the rest by the server.</summary>
        public int Id { get; set; }
        public NetEntityKind Kind { get; private set; }

        public Health Health { get; private set; }
        public EnemyBase Enemy { get; private set; }
        public BossBase Boss { get; private set; }
        public Boulder Boulder { get; private set; }
        public LootPickup Loot { get; private set; }
        public TreasureChest Chest { get; private set; }

        SpriteAnimator anim;
        SpriteRenderer body;
        StatusEffects status;
        Poise poise;
        Transform lift;
        // the body's colliders and which were triggers to begin with (a copy lets heroes through when the server's does)
        Collider2D[] solids;
        bool[] triggerAtStart;
        Collider2D mainSolid;
        bool intangible;

        // server: what the snapshots last said
        internal EntityState lastSent;
        internal bool everSent;
        internal float lastSentAt = -99f;

        // client: recent positions from the server, to draw a little behind it
        struct Sample
        {
            public double t;
            public Vector2 pos;
            public float lift;
        }

        const int Samples = 8;
        readonly Sample[] samples = new Sample[Samples];
        int sampleCount;
        int lastClip = -2;
        int lastSerial = -1;
        bool applied;
        bool wasDead;

        /// <summary>Seconds a client draws behind the server, so there are always two positions to move between.</summary>
        public const float InterpolationDelay = 0.12f;
        /// <summary>A jump further than this is a teleport, not a walk.</summary>
        const float SnapDistance = 4f;

        public static NetEntity Attach(GameObject go, NetEntityKind kind)
        {
            var e = go.GetComponent<NetEntity>();
            if (e == null) e = go.AddComponent<NetEntity>();
            e.Setup(kind);
            return e;
        }

        void Setup(NetEntityKind kind)
        {
            Kind = kind;
            Health = GetComponent<Health>();
            Enemy = GetComponent<EnemyBase>();
            Boss = GetComponent<BossBase>();
            Boulder = GetComponent<Boulder>();
            Loot = GetComponent<LootPickup>();
            Chest = GetComponent<TreasureChest>();
            status = GetComponent<StatusEffects>();
            poise = GetComponent<Poise>();
            if (Enemy != null)
            {
                anim = Enemy.anim;
                body = Enemy.body;
            }
            else if (Boss != null)
            {
                anim = Boss.anim;
                body = Boss.body;
                lift = Boss.bodyRoot;
            }
            if (anim == null) anim = GetComponentInChildren<SpriteAnimator>();
            if (body == null && anim != null) body = anim.target;
            solids = GetComponentsInChildren<Collider2D>(true);
            triggerAtStart = new bool[solids.Length];
            for (int i = 0; i < solids.Length; i++)
            {
                triggerAtStart[i] = solids[i].isTrigger;
                if (mainSolid == null && !solids[i].isTrigger) mainSolid = solids[i];
            }
            if (GameSession.IsAuthority) return;
            // a client's copy is moved by the server, not by physics or its own feet
            var motor = GetComponent<CharacterMotor>();
            if (motor != null)
            {
                motor.HardStop();
                motor.enabled = false;
            }
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            if (status != null) status.drivesAnimation = false;   // the server's animation speed arrives with the snapshots
        }

        // ================================================================== server
        public EntityState Capture()
        {
            var s = new EntityState
            {
                id = Id,
                pos = transform.position,
                lift = lift != null ? lift.localPosition.y : 0f,
                clip = (short)(anim != null ? anim.ClipIndex : -1),
                serial = (byte)(anim != null ? anim.PlayCount & 0xff : 0),
                speed = (byte)Mathf.Clamp(Mathf.RoundToInt((anim != null ? anim.speed : 1f) * 20f), 0, 255),
                hp = Health != null ? Health.hp : 0f,
                maxHp = Health != null ? Health.maxHp : 0f,
                status = status != null ? status.CaptureView() : default
            };
            byte f = 0;
            if (body != null && body.flipX) f |= EntityFlags.FlipX;
            if (Health != null && Health.IsDead) f |= EntityFlags.Dead;
            else if (mainSolid != null && (!mainSolid.enabled || mainSolid.isTrigger)) f |= EntityFlags.Intangible;
            if (gameObject.activeInHierarchy) f |= EntityFlags.Visible;
            if (Boss != null)
            {
                if (Boss.Engaged) f |= EntityFlags.Engaged;
                if (Boss.Enraged) f |= EntityFlags.Enraged;
            }
            if (poise != null)
            {
                s.poise = (byte)Mathf.RoundToInt(poise.Fraction * 255f);
                if (poise.IsBroken) f |= EntityFlags.PoiseBroken;
            }
            s.flags = f;
            return s;
        }

        /// <summary>Whether <paramref name="now"/> differs enough from what was last sent to be worth sending.</summary>
        public static bool Differs(EntityState a, EntityState b)
        {
            if (a.flags != b.flags || a.clip != b.clip || a.serial != b.serial || a.speed != b.speed) return true;
            if ((a.pos - b.pos).sqrMagnitude > 0.0004f || Mathf.Abs(a.lift - b.lift) > 0.01f) return true;
            if (Mathf.Abs(a.hp - b.hp) > 0.01f || Mathf.Abs(a.maxHp - b.maxHp) > 0.01f || a.poise != b.poise) return true;
            var x = a.status;
            var y = b.status;
            return x.stun != y.stun || x.freeze != y.freeze || x.root != y.root || x.slow != y.slow || x.chill != y.chill ||
                   x.charge != y.charge || x.burn != y.burn || x.poison != y.poison || x.flags != y.flags;
        }

        // ================================================================== client
        /// <summary>A snapshot arrived: remember the position, apply the rest at once.</summary>
        public void Apply(EntityState s, double serverTime)
        {
            bool visible = (s.flags & EntityFlags.Visible) != 0;
            bool dead = (s.flags & EntityFlags.Dead) != 0;
            if (!visible)
            {
                // fallen and faded on the server: gone here too (after its own fall, if it was seen)
                if (gameObject.activeSelf && (!wasDead || !applied || Kind == NetEntityKind.Boss)) Hide();
                wasDead = dead;
                applied = true;
                return;
            }
            if (!gameObject.activeSelf)
            {
                // back on the server (a camp respawned, the boss returned): here too, at its new place
                transform.position = s.pos;
                sampleCount = 0;
                gameObject.SetActive(true);
                lastClip = -2;
                wasDead = false;
            }
            AddSample(serverTime, s.pos, s.lift);
            if (!applied) transform.position = s.pos;
            if (body != null) body.flipX = (s.flags & EntityFlags.FlipX) != 0;
            if (Health != null) Health.SetRemote(s.hp, s.maxHp);
            if (status != null) status.ApplyView(s.status);
            if (poise != null) poise.SetView(s.poise / 255f, (s.flags & EntityFlags.PoiseBroken) != 0);
            if (Boss != null) Boss.SetRemoteFlags((s.flags & EntityFlags.Engaged) != 0, (s.flags & EntityFlags.Enraged) != 0);
            if (anim != null)
            {
                if (s.clip >= 0 && (s.clip != lastClip || s.serial != lastSerial))
                    anim.PlayIndex(s.clip, s.serial != lastSerial);
                anim.speed = s.speed / 20f;
                lastClip = s.clip;
                lastSerial = s.serial;
            }
            if (dead && !wasDead && Health != null) Health.SetRemoteDead(true);
            else if (!dead && wasDead && Health != null) Health.SetRemoteDead(false);
            SetIntangible((s.flags & EntityFlags.Intangible) != 0);
            wasDead = dead;
            applied = true;
        }

        /// <summary>A copy's body lets heroes through while the server's does (triggers: still hit by attacks).</summary>
        void SetIntangible(bool on)
        {
            if (on == intangible || solids == null) return;
            intangible = on;
            for (int i = 0; i < solids.Length; i++)
                if (solids[i] != null) solids[i].isTrigger = on || triggerAtStart[i];
        }

        void Hide()
        {
            if (Health != null && !Health.IsDead) Health.SetRemoteDead(true, false);
            gameObject.SetActive(false);
        }

        void AddSample(double t, Vector2 pos, float h)
        {
            if (sampleCount > 0 && t <= samples[sampleCount - 1].t) return;   // late or repeated
            if (sampleCount == Samples)
            {
                System.Array.Copy(samples, 1, samples, 0, Samples - 1);
                sampleCount--;
            }
            samples[sampleCount++] = new Sample { t = t, pos = pos, lift = h };
        }

        void LateUpdate()
        {
            if (GameSession.IsAuthority || sampleCount == 0) return;
            double renderAt = NetWorld.ServerNow - InterpolationDelay;
            Sample a = samples[0], b = samples[0];
            if (renderAt >= samples[sampleCount - 1].t) a = b = samples[sampleCount - 1];
            else
            {
                for (int i = sampleCount - 1; i > 0; i--)
                {
                    if (samples[i - 1].t <= renderAt)
                    {
                        a = samples[i - 1];
                        b = samples[i];
                        break;
                    }
                }
            }
            float k = b.t > a.t ? (float)((renderAt - a.t) / (b.t - a.t)) : 1f;
            k = Mathf.Clamp01(k);
            Vector2 pos = Vector2.Lerp(a.pos, b.pos, k);
            if (((Vector2)transform.position - pos).sqrMagnitude > SnapDistance * SnapDistance) transform.position = b.pos;
            else transform.position = new Vector3(pos.x, pos.y, transform.position.z);
            if (lift != null) lift.localPosition = new Vector3(0f, Mathf.Lerp(a.lift, b.lift, k), 0f);
        }
    }
}
