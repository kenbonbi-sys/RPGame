using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Cột Gió (Thảo Nguyên Gió, plan §10): a column of rising air in a ring of standing stones at
    /// the rim of Khe Vực, the ravine no one can walk across. A hero stepping into it is lifted and
    /// carried over the ravine to its partner on the other rim (the partner carries back). The hero
    /// who moves (offline the hero, online its own player's machine) flies: the ravine's edges let
    /// them through while they are in the air, the body rides an arc above its shadow, and they come
    /// down just past the far ring. A server knows a hero near a column may cover ground fast
    /// (<see cref="Near"/>, its move check). Every other screen sees that hero glide over the ravine
    /// (another player's hero is moved by the network) and draws the rest itself: the gust, the arc
    /// as far as the hero has come across, the landing (<see cref="WatchOthers"/>).
    /// </summary>
    public class WindColumn : MonoBehaviour
    {
        public string columnId = "north_east";
        [Tooltip("Where this column carries to, on the other rim.")]
        public WindColumn partner;
        [Tooltip("The ravine's colliders: a hero in the air passes over them.")]
        public Collider2D[] ravine = new Collider2D[0];
        [Tooltip("How close to its middle a hero is taken up.")]
        public float radius = 1.1f;

        /// <summary>Units a second a carried hero flies (the server's move check allows a crossing).</summary>
        public const float CarrySpeed = 8f;
        /// <summary>How high above its shadow the body rises in the middle of the flight.</summary>
        public const float CarryHeight = 1.8f;
        /// <summary>How close to a column the server lets a hero cover a crossing.</summary>
        public const float NearRadius = 3.5f;

        public static readonly List<WindColumn> All = new List<WindColumn>();

        /// <summary>The hero on this screen is in the air right now.</summary>
        public static bool Carrying { get; private set; }

        PlayerController carriedHero;
        Transform carriedBody;
        Vector3 bodyRest;
        Collider2D[] carriedColliders;
        List<Collider2D> crossedRavines;
        bool carriesLocal;

        void OnEnable() => All.Add(this);

        void OnDisable()
        {
            All.Remove(this);
            StopAllCoroutines();
            EndCarry(false);
            foreach (var f in others) EndOther(f);
            others.Clear();
            var mine = new List<PlayerController>();
            foreach (var w in Watched)
                if (w.Value == this || w.Key == null) mine.Add(w.Key);
            foreach (var h in mine) Watched.Remove(h);
        }

        /// <summary>Where a hero coming from the partner lands: just past this ring, away from the ravine.</summary>
        public Vector2 Landing
        {
            get
            {
                if (partner == null) return (Vector2)transform.position + Vector2.down * 1.6f;
                Vector2 away = ((Vector2)transform.position - (Vector2)partner.transform.position).normalized;
                return (Vector2)transform.position + away * 1.7f;
            }
        }

        /// <summary>A column within <paramref name="dist"/> of a point, or null (the server's move check).</summary>
        public static WindColumn Near(Vector2 pos, float dist = NearRadius)
        {
            foreach (var c in All)
                if (c != null && Vector2.Distance(c.transform.position, pos) <= dist) return c;
            return null;
        }

        void Update()
        {
            if (GameSession.HasScreen) WatchOthers();
            var me = Players.Local;
            if (me == null || partner == null || Carrying || me.IsDead || me.motor == null || !me.motor.enabled || me.motor.IsDashing || me.motor.Rooted) return;
            if (Vector2.Distance(me.transform.position, transform.position) > radius) return;
            StartCoroutine(Carry(me));
        }

        /// <summary>Carries <paramref name="hero"/> to the partner (tests call it; the hero moving is its own machine's).</summary>
        public IEnumerator Carry(PlayerController hero)
        {
            if (hero == null || hero.IsDead || partner == null || carriedHero != null || Carrying || hero.motor == null || !hero.motor.enabled || hero.motor.Rooted) yield break;
            carriedHero = hero;
            carriesLocal = hero.IsLocal;
            if (carriesLocal) Carrying = true;
            Vector2 from = hero.transform.position;
            Vector2 to = partner.Landing;
            float seconds = Mathf.Max(0.5f, Vector2.Distance(from, to) / CarrySpeed);
            carriedColliders = hero.GetComponentsInChildren<Collider2D>();
            crossedRavines = ravine != null ? new List<Collider2D>(ravine) : new List<Collider2D>();
            if (partner.ravine != null) crossedRavines.AddRange(partner.ravine);
            SetPassing(carriedColliders, crossedRavines, true);
            // this screen's own presentation: every other screen draws the flight itself (WatchOthers)
            Gust(from);
            // the body rides an arc above its shadow
            carriedBody = hero.anim != null ? hero.anim.transform : null;
            bodyRest = carriedBody != null ? carriedBody.localPosition : Vector3.zero;
            hero.motor.HardStop();
            // flown by the motor at a steady speed (the network and the physics see a fast walk, not a jump)
            hero.motor.Dash((to - from) / seconds, seconds);
            bool landed = false;
            try
            {
                float t = 0f;
                while (t < seconds && hero != null && !hero.IsDead)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / seconds);
                    if (carriedBody != null) carriedBody.localPosition = bodyRest + Vector3.up * (Mathf.Sin(k * Mathf.PI) * CarryHeight);
                    yield return null;
                }
                if (hero != null && !hero.IsDead)
                {
                    if (Vector2.Distance(hero.transform.position, to) > 0.3f) hero.motor.Teleport(to);
                    Land(to);
                    landed = true;
                }
            }
            finally
            {
                EndCarry(landed);
            }
        }

        // Disabling/unloading a column stops its coroutine; it must not leave the hero raised,
        // able to walk through the ravine, or block every column for the rest of the session.
        void EndCarry(bool landed)
        {
            if (carriedBody != null) carriedBody.localPosition = bodyRest;
            if (carriedHero != null && carriedHero.motor != null)
            {
                carriedHero.motor.HardStop();
                var zone = ZoneRoot.Current;
                if (!landed && !carriedHero.IsDead && zone != null && zone.IsChasm(carriedHero.transform.position))
                    carriedHero.motor.Teleport(Landing);
            }
            if (carriedColliders != null && crossedRavines != null) SetPassing(carriedColliders, crossedRavines, false);
            if (carriesLocal) Carrying = false;
            carriedHero = null;
            carriedBody = null;
            carriedColliders = null;
            crossedRavines = null;
            carriesLocal = false;
        }

        static void Gust(Vector2 at)
        {
            if (!GameSession.HasScreen) return;
            VFX.Spawn("dash_burst", at, Quaternion.identity, 1.2f);
            AudioManager.Play("sfx_gust", 0.9f, 0.05f, at);
        }

        static void Land(Vector2 at)
        {
            if (!GameSession.HasScreen) return;
            VFX.Spawn("step_dust", at, Quaternion.identity, 1.4f);
            AudioManager.Play("sfx_step", 0.7f, 0.1f, at);
        }

        // ------------------------------------------------------------------ other players' flights
        /// <summary>Another player's hero seen crossing from this column, drawn by where it has got to.</summary>
        class Other
        {
            public PlayerController hero;
            public Transform body;
            public Vector3 rest;
            public Vector2 from, to;
            public float best, gainedAt, giveUpAt;
            public bool rising;
        }

        readonly List<Other> others = new List<Other>();
        /// <summary>Heroes of other players this screen is drawing (or has drawn) at a column: one column each, until they leave its ring.</summary>
        static readonly Dictionary<PlayerController, WindColumn> Watched = new Dictionary<PlayerController, WindColumn>();

        /// <summary>
        /// Another player's hero is flown by its own machine; here it only glides over the ravine
        /// (the network moves it). A hero stepping into this ring is watched: its body rises on the
        /// same arc as far as it has come across towards the far rim, with the gust as it goes up and
        /// the dust as it lands. One that stalls or turns away (it dashed through) is let go.
        /// </summary>
        void WatchOthers()
        {
            if (partner == null) return;
            foreach (var h in Players.All)
            {
                if (h == null || !h.Puppet || h.IsDead) continue;
                float dist = Vector2.Distance(h.transform.position, transform.position);
                if (Watched.TryGetValue(h, out var by) && by != null)
                {
                    // taken up again only after leaving the ring it was last seen in
                    if (by == this && dist > radius + 0.8f && !others.Exists(o => o.hero == h)) Watched.Remove(h);
                    continue;
                }
                if (dist > radius + 0.4f) continue;
                Watched[h] = this;
                others.Add(new Other
                {
                    hero = h,
                    body = h.anim != null ? h.anim.transform : null,
                    rest = h.anim != null ? h.anim.transform.localPosition : Vector3.zero,
                    from = h.transform.position,
                    to = partner.Landing,
                    gainedAt = Time.time,
                    giveUpAt = Time.time + Vector2.Distance(h.transform.position, partner.Landing) / CarrySpeed * 2f + 1.5f,
                });
            }
            for (int i = others.Count - 1; i >= 0; i--)
                if (!Follow(others[i]))
                {
                    EndOther(others[i]);
                    others.RemoveAt(i);
                }
        }

        /// <summary>Raises a watched hero's body by how far across it is; false when its flight is over.</summary>
        bool Follow(Other o)
        {
            var h = o.hero;
            if (h == null || h.IsDead || !h.Puppet || Time.time > o.giveUpAt) return false;
            Vector2 path = o.to - o.from;
            float len = path.magnitude;
            if (len < 0.5f) return false;
            Vector2 d = (Vector2)h.transform.position - o.from;
            float k = Mathf.Clamp01(Vector2.Dot(d, path) / (len * len));
            float off = Mathf.Abs(d.x * path.y - d.y * path.x) / len;
            if (off > 2.5f) return false;   // went elsewhere: not a flight
            if (k > o.best + 0.01f)
            {
                o.best = k;
                o.gainedAt = Time.time;
            }
            else if (Time.time - o.gainedAt > 0.6f) return false;   // stood still in the ring
            if (!o.rising && k > 0.05f)
            {
                o.rising = true;
                Gust(o.from);
            }
            if (o.body != null) o.body.localPosition = o.rest + Vector3.up * (Mathf.Sin(k * Mathf.PI) * CarryHeight);
            if (k < 0.97f) return true;
            Land(o.to);
            return false;
        }

        static void EndOther(Other o)
        {
            if (o.body != null) o.body.localPosition = o.rest;
        }

        static void SetPassing(Collider2D[] own, List<Collider2D> ravines, bool on)
        {
            foreach (var a in own)
                foreach (var b in ravines)
                    if (a != null && b != null) Physics2D.IgnoreCollision(a, b, on);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
            if (partner != null) Gizmos.DrawLine(transform.position, partner.transform.position);
        }
    }
}
