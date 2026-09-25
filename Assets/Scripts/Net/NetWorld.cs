using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The shared world of an online session (online phase 3, Docs/KeHoach-Online.md). The server
    /// runs enemies, the boss, rocks and loot exactly as offline; this sends what changed about
    /// 15 times a second (positions, animations, health, statuses), every hit, heal, skill and
    /// buff as it happens, and the cues of <see cref="NetCues"/>. A player's machine applies all
    /// of it to its own copy of the zone: its copies never think, they only show.
    /// Each player only gets what is within <see cref="ServerPlayers.ViewRadius"/> of their hero
    /// (an object coming into view is sent whole at once), so a bigger world costs no more per
    /// player; every hero's numbers go to everyone (party panels).
    /// Ids: a hero is its NetworkObject id + 1; the zone's objects are numbered in scene order
    /// from <see cref="NetProtocol.SceneIdBase"/> (the same on every machine); what the server
    /// makes while playing counts from <see cref="NetProtocol.DynamicIdBase"/>.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class NetWorld : MonoBehaviour
    {
        public static NetWorld I { get; private set; }

        /// <summary>Snapshots per second.</summary>
        public const float SnapshotRate = 15f;
        /// <summary>Everything is sent again at least this often, in case a snapshot was lost.</summary>
        const float RefreshSeconds = 1f;
        /// <summary>Entries per snapshot message: keeps each under one network packet.</summary>
        const int ChunkSize = 20;

        NetworkManager nm;
        bool serving, client;

        readonly Dictionary<int, NetEntity> entities = new Dictionary<int, NetEntity>();
        readonly List<NetEntity> order = new List<NetEntity>();
        readonly HashSet<int> goneSceneIds = new HashSet<int>();
        int nextDynamicId = NetProtocol.DynamicIdBase;
        bool zoneRegistered;

        // server
        float nextSnapshot;
        readonly List<HitMsg> pendingHits = new List<HitMsg>();
        readonly Dictionary<Health, int> pendingHitIndex = new Dictionary<Health, int>();
        readonly Dictionary<int, HeroState> lastHeroSent = new Dictionary<int, HeroState>();
        readonly Dictionary<int, float> lastHeroSentAt = new Dictionary<int, float>();
        readonly List<EntityState> entityBuffer = new List<EntityState>();
        readonly List<HeroState> heroBuffer = new List<HeroState>();
        // this tick's state of every object, and whether it changed since the last tick
        readonly List<(int id, EntityState state, bool changed)> tickStates = new List<(int, EntityState, bool)>();
        readonly List<(NetworkConnection conn, Vector2 at)> viewers = new List<(NetworkConnection, Vector2)>();
        // per player: the objects in their view at the last snapshot (one coming into view is sent whole)
        readonly Dictionary<int, HashSet<int>> seenBy = new Dictionary<int, HashSet<int>>();
        readonly HashSet<int> liveViewers = new HashSet<int>();
        readonly List<int> goneViewers = new List<int>();

        // client
        double clockOffset;
        bool clockKnown;
        double lastSnapshotTime;
        readonly Dictionary<int, string> heroNames = new Dictionary<int, string>();
        readonly Dictionary<int, string> heroLooks = new Dictionary<int, string>();

        /// <summary>The server's clock as this machine estimates it (the server's own clock on the server).</summary>
        public static double ServerNow => I != null && I.client && I.clockKnown
            ? Time.realtimeSinceStartupAsDouble + I.clockOffset
            : Time.realtimeSinceStartupAsDouble;

        /// <summary>Seconds a message takes from the server to here (half the ping); 0 on the server.</summary>
        public static float OneWayDelay => I != null && I.client && I.nm != null ? Mathf.Clamp(I.nm.TimeManager.RoundTripTime / 2000f, 0f, 0.4f) : 0f;

        void Awake() => I = this;

        void OnDestroy()
        {
            End();
            if (I == this) I = null;
        }

        // ================================================================== session
        /// <summary>Hooks into a started network: <paramref name="asServer"/> sends the world, a client applies it.</summary>
        public void Begin(NetworkManager manager)
        {
            nm = manager;
            serving = GameSession.Serving;
            client = GameSession.Mode == SessionMode.Client;
            if (serving)
            {
                GameEvents.Damaged += OnDamaged;
                GameEvents.Healed += OnHealed;
            }
            if (client)
            {
                var c = nm.ClientManager;
                c.RegisterBroadcast<WorldSnapshot>(OnSnapshot);
                c.RegisterBroadcast<EntitySpawned>(OnSpawned);
                c.RegisterBroadcast<EntityGone>(OnGone);
                c.RegisterBroadcast<HitMsg>(OnHit);
                c.RegisterBroadcast<HealMsg>(OnHeal);
                c.RegisterBroadcast<CastMsg>(OnCast);
                c.RegisterBroadcast<BuffMsg>(OnBuff);
                c.RegisterBroadcast<CueMsg>(OnCue);
                c.RegisterBroadcast<HeroInfoMsg>(OnHeroInfo);
                c.RegisterBroadcast<ChatMsg>(OnChat);
            }
            if (SceneLoader.I != null && !SceneLoader.I.Busy && ZoneRoot.Current != null) StartCoroutine(RegisterZone(ZoneRoot.Current));
        }

        public void End()
        {
            if (nm == null) return;
            if (serving)
            {
                GameEvents.Damaged -= OnDamaged;
                GameEvents.Healed -= OnHealed;
            }
            if (client && nm.ClientManager != null)
            {
                var c = nm.ClientManager;
                c.UnregisterBroadcast<WorldSnapshot>(OnSnapshot);
                c.UnregisterBroadcast<EntitySpawned>(OnSpawned);
                c.UnregisterBroadcast<EntityGone>(OnGone);
                c.UnregisterBroadcast<HitMsg>(OnHit);
                c.UnregisterBroadcast<HealMsg>(OnHeal);
                c.UnregisterBroadcast<CastMsg>(OnCast);
                c.UnregisterBroadcast<BuffMsg>(OnBuff);
                c.UnregisterBroadcast<CueMsg>(OnCue);
                c.UnregisterBroadcast<HeroInfoMsg>(OnHeroInfo);
                c.UnregisterBroadcast<ChatMsg>(OnChat);
            }
            nm = null;
        }

        /// <summary>
        /// Numbers the zone's enemies, bosses, rocks and crystal pillars in scene order, a frame after the zone
        /// loaded (so camps have made their enemies). Every machine loads the same scene, so the
        /// numbers match.
        /// </summary>
        public IEnumerator RegisterZone(ZoneRoot zone)
        {
            if (zoneRegistered || zone == null) yield break;
            zoneRegistered = true;
            yield return null;
            int n = 0;
            foreach (var root in zone.Scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    NetEntityKind kind;
                    if (t.GetComponent<BossBase>() != null) kind = NetEntityKind.Boss;
                    else if (t.GetComponent<EnemyBase>() != null) kind = NetEntityKind.Enemy;
                    else if (t.GetComponent<Boulder>() != null) kind = NetEntityKind.Boulder;
                    else if (t.GetComponent<CrystalPillar>() != null) kind = NetEntityKind.Pillar;
                    else continue;
                    var e = NetEntity.Attach(t.gameObject, kind);
                    e.Id = NetProtocol.SceneIdBase + ++n;
                    Add(e);
                }
            }
            Debug.Log($"[Online] {n} zone objects shared");
        }

        void Add(NetEntity e)
        {
            entities[e.Id] = e;
            order.Add(e);
        }

        /// <summary>A replicated object is gone for good (destroyed or given back to the pool).</summary>
        public static void Forget(NetEntity e, GoneHow how = GoneHow.Removed, int by = 0)
        {
            var w = I;
            if (w == null || e == null || !w.entities.Remove(e.Id)) return;
            w.order.Remove(e);
            if (!w.serving) return;
            if (e.Id < NetProtocol.DynamicIdBase) w.goneSceneIds.Add(e.Id);
            var msg = new EntityGone { id = e.Id, how = how, by = by };
            if (e.Kind == NetEntityKind.Loot) ServerPlayers.SendToOwnerOf(e.Loot, msg);
            else if (e.Kind == NetEntityKind.Chest) { if (e.Chest != null && e.Chest.owner != null) ServerPlayers.SendTo(e.Chest.owner, msg); }
            else ServerPlayers.SendToAll(msg);
        }

        // ================================================================== ids
        /// <summary>The network id of a hero or a replicated object (0 = none).</summary>
        public static int IdOf(Component c)
        {
            if (c == null) return 0;
            var e = c.GetComponentInParent<NetEntity>();
            if (e != null) return e.Id;
            var nob = c.GetComponentInParent<NetworkObject>();
            return nob != null && nob.IsSpawned ? nob.ObjectId + 1 : 0;
        }

        public static int IdOf(GameObject go) => go != null ? IdOf(go.transform) : 0;

        /// <summary>The hero or replicated object with that id, or null.</summary>
        public static Component Find(int id)
        {
            if (id <= 0) return null;
            if (id >= NetProtocol.SceneIdBase)
                return I != null && I.entities.TryGetValue(id, out var e) && e != null ? e : null;
            return FindHero(id);
        }

        public static PlayerController FindHero(int id)
        {
            foreach (var p in Players.All)
            {
                if (p == null) continue;
                var nob = p.GetComponent<NetworkObject>();
                if (nob != null && nob.IsSpawned && nob.ObjectId + 1 == id) return p;
            }
            return null;
        }

        /// <summary>The name of a hero (its player's character).</summary>
        public static string HeroName(int id) => I != null && I.heroNames.TryGetValue(id, out var n) ? n : null;

        /// <summary>The look of a hero as the server last told (null: not known yet).</summary>
        public static string HeroLookOf(int id) => I != null && I.heroLooks.TryGetValue(id, out var l) ? l : null;

        // ================================================================== server: sending
        void Update()
        {
            if (!serving || nm == null || !nm.ServerManager.Started) return;
            if (Time.unscaledTime < nextSnapshot) return;
            nextSnapshot = Time.unscaledTime + 1f / SnapshotRate;
            SendSnapshot(null, false);
        }

        void LateUpdate()
        {
            if (!serving || pendingHits.Count == 0) return;
            foreach (var h in pendingHits) ServerPlayers.SendNear(h, h.point, ServerPlayers.ViewRadius);
            pendingHits.Clear();
            pendingHitIndex.Clear();
        }

        /// <summary>
        /// Sends what changed to every player, each only the objects in their view (one that just
        /// came into view whole); or, for <paramref name="to"/>, everything: a player who just
        /// arrived. Unreliable: a lost one is covered by the next, and everything is sent again
        /// every <see cref="RefreshSeconds"/>.
        /// </summary>
        void SendSnapshot(NetworkConnection to, bool full)
        {
            float now = Time.unscaledTime;
            tickStates.Clear();
            heroBuffer.Clear();
            foreach (var e in order)
            {
                if (e == null || e.Kind == NetEntityKind.Loot || e.Kind == NetEntityKind.Chest) continue;
                var s = e.Capture();
                bool changed = full || !e.everSent || NetEntity.Differs(s, e.lastSent) || now - e.lastSentAt >= RefreshSeconds;
                tickStates.Add((e.Id, s, changed));
                if (to != null || !changed) continue;
                e.lastSent = s;
                e.everSent = true;
                e.lastSentAt = now;
            }
            foreach (var p in Players.All)
            {
                int id = IdOf(p);
                if (id <= 0) continue;
                var s = CaptureHero(p, id);
                if (!full && lastHeroSent.TryGetValue(id, out var was) && !HeroDiffers(s, was) &&
                    lastHeroSentAt.TryGetValue(id, out float at) && now - at < RefreshSeconds) continue;
                heroBuffer.Add(s);
                if (to != null) continue;
                lastHeroSent[id] = s;
                lastHeroSentAt[id] = now;
            }
            double time = Time.realtimeSinceStartupAsDouble;
            float day = DayNightCycle.I != null ? DayNightCycle.I.time : -1f;
            if (to != null)
            {
                // a player who just arrived: the whole world once (then only what they see)
                entityBuffer.Clear();
                foreach (var t in tickStates) entityBuffer.Add(t.state);
                Send(to, time, day, Channel.Reliable, true);
                seenBy.Remove(to.ClientId);
                return;
            }
            ServerPlayers.Viewers(viewers);
            liveViewers.Clear();
            float sq = ServerPlayers.ViewRadius * ServerPlayers.ViewRadius;
            foreach (var v in viewers)
            {
                liveViewers.Add(v.conn.ClientId);
                if (!seenBy.TryGetValue(v.conn.ClientId, out var seen)) seenBy[v.conn.ClientId] = seen = new HashSet<int>();
                entityBuffer.Clear();
                foreach (var t in tickStates)
                {
                    if ((t.state.pos - v.at).sqrMagnitude > sq)
                    {
                        seen.Remove(t.id);   // out of view: sent whole when it comes back
                        continue;
                    }
                    bool entering = seen.Add(t.id);
                    if (t.changed || entering) entityBuffer.Add(t.state);
                }
                Send(v.conn, time, day, Channel.Unreliable, false);
            }
            // players who left
            goneViewers.Clear();
            foreach (var id in seenBy.Keys)
                if (!liveViewers.Contains(id)) goneViewers.Add(id);
            foreach (var id in goneViewers) seenBy.Remove(id);
        }

        /// <summary>The objects in <see cref="entityBuffer"/> and the heroes in <see cref="heroBuffer"/>, in packet-sized parts.</summary>
        void Send(NetworkConnection to, double time, float day, Channel channel, bool evenEmpty)
        {
            if (entityBuffer.Count == 0 && heroBuffer.Count == 0 && !evenEmpty) return;
            int chunks = Mathf.Max(1, Mathf.CeilToInt(entityBuffer.Count / (float)ChunkSize));
            for (int c = 0; c < chunks; c++)
            {
                int from = c * ChunkSize;
                int count = Mathf.Clamp(entityBuffer.Count - from, 0, ChunkSize);
                var msg = new WorldSnapshot
                {
                    time = time,
                    dayTime = day,
                    entities = entityBuffer.GetRange(from, count).ToArray(),
                    heroes = c == 0 ? heroBuffer.ToArray() : new HeroState[0]
                };
                nm.ServerManager.Broadcast(to, msg, true, channel);
            }
        }

        static HeroState CaptureHero(PlayerController p, int id)
        {
            var h = p.health;
            byte flags = 0;
            if (p.IsDead) flags |= 1;
            if (h != null && h.invulnerable) flags |= 2;
            return new HeroState
            {
                id = id,
                hp = h != null ? h.hp : 0f,
                maxHp = h != null ? h.maxHp : 0f,
                energy = p.energy,
                maxEnergy = p.maxEnergy,
                level = (short)(p.stats != null ? p.stats.level : 1),
                flags = flags,
                status = p.status != null ? p.status.CaptureView() : default
            };
        }

        static bool HeroDiffers(HeroState a, HeroState b) =>
            Mathf.Abs(a.hp - b.hp) > 0.01f || Mathf.Abs(a.maxHp - b.maxHp) > 0.01f || Mathf.Abs(a.energy - b.energy) > 0.25f ||
            Mathf.Abs(a.maxEnergy - b.maxEnergy) > 0.01f || a.level != b.level || a.flags != b.flags ||
            a.status.stun != b.status.stun || a.status.freeze != b.status.freeze || a.status.root != b.status.root ||
            a.status.slow != b.status.slow || a.status.chill != b.status.chill || a.status.charge != b.status.charge ||
            a.status.burn != b.status.burn || a.status.poison != b.status.poison || a.status.flags != b.status.flags;

        /// <summary>
        /// Everything a player who just arrived needs: the rocks thrown before, the zone objects
        /// already gone, every hero's name, and the whole state of the world.
        /// </summary>
        public void SendEverything(NetworkConnection to)
        {
            if (!serving || to == null) return;
            foreach (var id in goneSceneIds) nm.ServerManager.Broadcast(to, new EntityGone { id = id }, true);
            foreach (var e in order)
            {
                if (e == null || e.Id < NetProtocol.DynamicIdBase) continue;
                if (e.Kind == NetEntityKind.Boulder)
                    nm.ServerManager.Broadcast(to, new EntitySpawned { id = e.Id, kind = SpawnKind.Boulder, pos = e.transform.position }, true);
                else if (e.Kind == NetEntityKind.Loot && e.Loot != null && ServerPlayers.ConnectionOf(e.Loot.owner) == to)
                    nm.ServerManager.Broadcast(to, LootMessage(e), true);
                else if (e.Kind == NetEntityKind.Chest && e.Chest != null && !e.Chest.Opened && ServerPlayers.ConnectionOf(e.Chest.owner) == to)
                    nm.ServerManager.Broadcast(to, new EntitySpawned { id = e.Id, kind = SpawnKind.Chest, pos = e.transform.position }, true);
            }
            foreach (var p in Players.All)
            {
                string name = ServerPlayers.NameOf(p);
                int id = IdOf(p);
                if (name != null && id > 0)
                    nm.ServerManager.Broadcast(to, new HeroInfoMsg { hero = id, name = name, look = p.stats != null ? p.stats.look.ToJson() : null }, true);
            }
            SendSnapshot(to, true);
        }

        static EntitySpawned LootMessage(NetEntity e) => new EntitySpawned
        {
            id = e.Id, kind = SpawnKind.Loot, what = e.Loot.Item != null ? e.Loot.Item.id : "", count = e.Loot.Count,
            pos = e.Loot.From, land = e.Loot.Land
        };

        // ------------------------------------------------------------------ server: what happened
        void OnDamaged(Health h, DamageInfo d, float amount)
        {
            int target = IdOf(h);
            if (target == 0) return;
            byte flags = 0;
            if (d.crit) flags |= HitFlags.Crit;
            if (d.dot) flags |= HitFlags.Dot;
            if (d.contact) flags |= HitFlags.Contact;
            if (d.pure) flags |= HitFlags.Pure;
            pendingHitIndex[h] = pendingHits.Count;
            pendingHits.Add(new HitMsg
            {
                target = target,
                source = IdOf(d.source),
                amount = amount,
                type = (byte)d.type,
                flags = flags,
                point = d.point,
                dir = d.direction,
                knockback = d.knockback,
                hitStop = d.hitStop,
                skill = d.skillName ?? ""
            });
        }

        /// <summary>The last hit on <paramref name="h"/> this frame also shows its knockback, flash and sparks.</summary>
        public static void MarkFeedback(Health h)
        {
            var w = I;
            if (w == null || h == null || !w.pendingHitIndex.TryGetValue(h, out int i)) return;
            var m = w.pendingHits[i];
            m.flags |= HitFlags.Feedback;
            w.pendingHits[i] = m;
        }

        void OnHealed(Health h, float amount)
        {
            int target = IdOf(h);
            if (target != 0) ServerPlayers.SendNear(new HealMsg { target = target, amount = amount, show = true }, h.transform.position, ServerPlayers.ViewRadius);
        }

        /// <summary>
        /// A cue for the players who could see or feel it: near where it happens (a projectile as
        /// far as it flies, a shake or a log line as far as its own reach); a boss's moments,
        /// cancelled warnings and sounds heard everywhere go to everyone.
        /// </summary>
        public static void SendCue(CueMsg m)
        {
            var kind = (NetCues.Kind)m.kind;
            if (kind == NetCues.Kind.Boss || kind == NetCues.Kind.TelegraphCancel || m.d >= float.MaxValue)
            {
                ServerPlayers.SendToAll(m);
                return;
            }
            float reach = ServerPlayers.ViewRadius;
            if (kind == NetCues.Kind.Projectile) reach += m.a * m.b;   // speed × lifetime
            else if (kind == NetCues.Kind.Arc || kind == NetCues.Kind.Beam) reach += Vector2.Distance(m.pos, m.pos2);
            else if (kind == NetCues.Kind.Shake || kind == NetCues.Kind.Flash || kind == NetCues.Kind.Impact ||
                     kind == NetCues.Kind.Log || kind == NetCues.Kind.Banner || kind == NetCues.Kind.FlatSound) reach = Mathf.Max(reach, m.d);
            ServerPlayers.SendNear(m, m.pos, reach);
        }

        /// <summary>A hero used a skill: everyone near but its own player (who already showed it) shows it.</summary>
        public static void CastShown(PlayerController hero, int slot, Vector2 aim, Vector2 origin, int level, int seed)
        {
            int id = IdOf(hero);
            if (id <= 0) return;
            ServerPlayers.SendNear(new CastMsg { hero = id, slot = (byte)slot, aim = aim, origin = origin, level = (byte)level, seed = seed },
                                   origin, ServerPlayers.ViewRadius, Channel.Reliable, hero);
        }

        /// <summary>A buff started or ended on a hero: every screen shows it (its player's buff bar too).</summary>
        public static void BuffChanged(PlayerController hero, string buffId, float remaining, bool on)
        {
            int id = IdOf(hero);
            if (id > 0) ServerPlayers.SendToAll(new BuffMsg { hero = id, buff = buffId, remaining = remaining, on = on });
        }

        /// <summary>A rock thrown by the boss stays on the field: every screen gets it.</summary>
        public static void BoulderMade(Boulder b)
        {
            var w = I;
            if (w == null || !w.serving || b == null) return;
            var e = NetEntity.Attach(b.gameObject, NetEntityKind.Boulder);
            e.Id = w.nextDynamicId++;
            w.Add(e);
            ServerPlayers.SendToAll(new EntitySpawned { id = e.Id, kind = SpawnKind.Boulder, pos = b.transform.position });
        }

        /// <summary>A loot drop: its owner's screen gets it (loot is personal); anyone's when it has no owner.</summary>
        public static void LootDropped(LootPickup loot)
        {
            var w = I;
            if (w == null || !w.serving || loot == null) return;
            var e = NetEntity.Attach(loot.gameObject, NetEntityKind.Loot);
            e.Id = w.nextDynamicId++;
            w.Add(e);
            var msg = LootMessage(e);
            if (loot.owner != null) ServerPlayers.SendToOwnerOf(loot, msg);
            else ServerPlayers.SendToAll(msg);
        }

        /// <summary>A fallen boss's chest: only the screen of the hero it holds loot for shows it (loot is personal).</summary>
        public static void ChestLeft(TreasureChest chest)
        {
            var w = I;
            if (w == null || !w.serving || chest == null) return;
            var e = NetEntity.Attach(chest.gameObject, NetEntityKind.Chest);
            e.Id = w.nextDynamicId++;
            w.Add(e);
            if (chest.owner != null)
                ServerPlayers.SendTo(chest.owner, new EntitySpawned { id = e.Id, kind = SpawnKind.Chest, pos = chest.transform.position });
        }

        /// <summary>A hero's name, for every screen (sent when the hero arrives).</summary>
        public static void AnnounceHero(PlayerController hero, string name)
        {
            int id = IdOf(hero);
            if (id <= 0) return;
            string look = hero.stats != null ? hero.stats.look.ToJson() : null;
            if (I != null)
            {
                I.heroNames[id] = name;
                if (look != null) I.heroLooks[id] = look;
            }
            ServerPlayers.SendToAll(new HeroInfoMsg { hero = id, name = name, look = look });
        }

        // ================================================================== client: applying
        void OnSnapshot(WorldSnapshot s, Channel channel)
        {
            double sample = s.time - Time.realtimeSinceStartupAsDouble;
            if (!clockKnown || sample > clockOffset) clockOffset = sample;
            else clockOffset -= 0.0005;   // drifts back toward the fastest path
            clockKnown = true;
            if (channel == Channel.Unreliable && s.time < lastSnapshotTime - 0.5) return;   // badly late
            lastSnapshotTime = System.Math.Max(lastSnapshotTime, s.time);
            if (s.dayTime >= 0f && DayNightCycle.I != null) DayNightCycle.I.SetFromServer(s.dayTime);
            if (s.entities != null)
                foreach (var st in s.entities)
                    if (entities.TryGetValue(st.id, out var e) && e != null) e.Apply(st, s.time);
            if (s.heroes != null)
                foreach (var hs in s.heroes)
                {
                    var hero = FindHero(hs.id);
                    if (hero != null) hero.ApplyServerState(hs);
                }
        }

        void OnSpawned(EntitySpawned m, Channel channel)
        {
            if (entities.ContainsKey(m.id)) return;
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null) return;
            NetEntity e = null;
            switch (m.kind)
            {
                case SpawnKind.Boulder:
                    if (db.boulderPrefab == null) return;
                    var parent = ZoneRoot.Current != null ? ZoneRoot.Current.transform : null;
                    var go = Instantiate(db.boulderPrefab, m.pos, Quaternion.identity, parent);
                    e = NetEntity.Attach(go, NetEntityKind.Boulder);
                    break;
                case SpawnKind.Loot:
                    var item = db.Item(m.what);
                    if (item == null || db.lootPrefab == null) return;
                    var lgo = Pool.Get(db.lootPrefab, m.pos, Quaternion.identity);
                    var loot = lgo.GetComponent<LootPickup>();
                    loot.Setup(item, m.count, m.pos, m.land, Players.Local, true);
                    e = NetEntity.Attach(lgo, NetEntityKind.Loot);
                    break;
                case SpawnKind.Chest:
                    if (db.chestPrefab == null) return;
                    var cgo = Instantiate(db.chestPrefab, m.pos, Quaternion.identity, ZoneRoot.Current != null ? ZoneRoot.Current.transform : null);
                    cgo.GetComponent<TreasureChest>().Show();
                    e = NetEntity.Attach(cgo, NetEntityKind.Chest);
                    break;
            }
            if (e == null) return;
            e.Id = m.id;
            Add(e);
        }

        void OnGone(EntityGone m, Channel channel)
        {
            if (!entities.TryGetValue(m.id, out var e) || e == null)
            {
                entities.Remove(m.id);
                return;
            }
            entities.Remove(m.id);
            order.Remove(e);
            switch (e.Kind)
            {
                case NetEntityKind.Loot:
                    if (e.Loot != null) e.Loot.RemoteGone(m.how == GoneHow.Collected ? FindHero(m.by) : null);
                    break;
                case NetEntityKind.Boulder:
                    Destroy(e.gameObject);
                    break;
                case NetEntityKind.Chest:
                    if (e.Chest != null && m.how == GoneHow.Collected) e.Chest.ShowOpened();
                    else Destroy(e.gameObject);
                    break;
                default:
                    e.gameObject.SetActive(false);
                    break;
            }
        }

        void OnHit(HitMsg m, Channel channel)
        {
            var target = Find(m.target);
            var h = target != null ? target.GetComponent<Health>() : null;
            if (h == null) return;
            var source = Find(m.source);
            var d = new DamageInfo
            {
                amount = m.amount,
                type = (DamageType)m.type,
                sourceTeam = h.team == Team.Player ? Team.Enemy : Team.Player,
                source = source != null ? source.gameObject : null,
                point = m.point,
                direction = m.dir,
                knockback = m.knockback,
                hitStop = m.hitStop,
                crit = (m.flags & HitFlags.Crit) != 0,
                dot = (m.flags & HitFlags.Dot) != 0,
                contact = (m.flags & HitFlags.Contact) != 0,
                pure = (m.flags & HitFlags.Pure) != 0,
                skillName = m.skill
            };
            if (source != null)
            {
                var sh = source.GetComponent<Health>();
                if (sh != null) d.sourceTeam = sh.team;
            }
            h.ReplayHit(d, m.amount);
            if ((m.flags & HitFlags.Feedback) != 0) Combat.OnHitFeedback(h, d);
        }

        void OnHeal(HealMsg m, Channel channel)
        {
            var target = Find(m.target);
            var h = target != null ? target.GetComponent<Health>() : null;
            if (h != null) h.ReplayHeal(m.amount, m.show);
        }

        void OnCast(CastMsg m, Channel channel)
        {
            var hero = FindHero(m.hero);
            if (hero != null && !hero.IsLocal) hero.skills.ShowCast(m.slot, m.aim, m.origin, m.level, m.seed);
        }

        void OnBuff(BuffMsg m, Channel channel)
        {
            var hero = FindHero(m.hero);
            if (hero == null) return;
            if (!m.on)
            {
                hero.RemoveBuff(m.buff);
                return;
            }
            var spec = Buffs.Find(m.buff);
            if (spec != null) hero.AddBuff(spec, m.remaining);
        }

        void OnCue(CueMsg m, Channel channel) => NetCues.Play(m, true);

        void OnHeroInfo(HeroInfoMsg m, Channel channel)
        {
            heroNames[m.hero] = m.name;
            if (!string.IsNullOrEmpty(m.look)) heroLooks[m.hero] = m.look;
            var hero = FindHero(m.hero);
            var net = hero != null ? hero.GetComponent<NetworkHero>() : null;
            if (net != null) net.SetDisplayName(m.name);
            // another player's hero is drawn (and given its skill bar) from its look; ours comes with our sheet
            if (hero != null && !hero.IsLocal && hero.stats != null && !string.IsNullOrEmpty(m.look))
            {
                var look = HeroLook.FromJson(m.look);
                if (!look.SameAs(hero.stats.look)) hero.stats.LoadLook(look);
            }
        }

        void OnChat(ChatMsg m, Channel channel) => ShowChat(m);

        static readonly Color PartyColor = new Color(0.55f, 1f, 0.62f);
        static readonly Color WhisperColor = new Color(1f, 0.66f, 0.88f);

        /// <summary>A chat line in the log, coloured by its channel: everyone, the party, a whisper.</summary>
        public static void ShowChat(ChatMsg m)
        {
            switch (m.channel)
            {
                case ChatChannel.Party:
                    if (m.system) GameEvents.RaiseLog("[Tổ đội] " + m.text, PartyColor);
                    else GameEvents.RaiseLog($"<color=#7dff9a>[Tổ đội] {m.from}:</color> {m.text}", new Color(0.86f, 1f, 0.88f));
                    return;
                case ChatChannel.Whisper:
                    bool mine = string.Equals(LoginCrypto.NormalizeName(m.from), LoginCrypto.NormalizeName(LoginInfo.Name), System.StringComparison.OrdinalIgnoreCase);
                    GameEvents.RaiseLog(mine ? $"<color=#ff9ad8>Bạn → {m.to}:</color> {m.text}" : $"<color=#ff9ad8>{m.from} → bạn:</color> {m.text}  <color=#b8b0c8>(/w {m.from} …)</color>", WhisperColor);
                    return;
                default:
                    if (m.system) GameEvents.RaiseLog(m.text, Palette.LogQuest);
                    else GameEvents.RaiseLog($"<color=#8fd3ff>{m.from}:</color> {m.text}", new Color(0.92f, 0.95f, 1f));
                    return;
            }
        }

        // ------------------------------------------------------------------ client: things only watched
        /// <summary>An enemy's projectile seen on this screen: it flies and bursts, the hit is the server's.</summary>
        public static void ShowProjectile(CueMsg m)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null) return;
            var prefab = m.id == "spore" ? db.sporePrefab : m.id == "venom" ? db.venomPrefab : m.id == "web" ? db.webPrefab
                       : m.id == "fireball" ? db.fireballPrefab : m.id == "shard" ? db.shardPrefab
                       : m.id == "arrow" ? db.arrowPrefab : m.id == "windblade" ? db.windBladePrefab : m.id == "tornado" ? db.tornadoPrefab : null;
            if (prefab == null) return;
            var go = Pool.Get(prefab, m.pos, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            var pr = go.GetComponent<Projectile>();
            if (pr == null)
            {
                Pool.Release(go);
                return;
            }
            string[] cues = (m.text ?? "").Split('|');
            pr.team = (Team)m.flag;
            pr.speed = m.a;
            pr.lifetime = m.b;
            pr.shake = m.c;
            pr.explodeRadius = m.d;
            // a wind blade or a whirlwind goes on through whoever it passes, on the server and here
            pr.pierce = m.id == "windblade" || m.id == "tornado";
            pr.hitVfx = cues.Length > 0 ? cues[0] : "";
            pr.hitSfx = cues.Length > 1 ? cues[1] : "";
            pr.Launch(m.pos2, null, false);
        }

        /// <summary>A lobbed rock or poison glob seen on this screen (its landing arrives as its own cues).</summary>
        public static void ShowArc(CueMsg m)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            var prefab = db == null ? null : m.id == "venom" ? db.venomArcPrefab : db.rockProjectilePrefab;
            if (prefab == null) return;
            var go = Pool.Get(prefab, m.pos, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            var arc = go.GetComponent<ArcProjectile>();
            if (arc != null) arc.Launch(m.pos, m.pos2, m.a, null);
        }
    }
}
