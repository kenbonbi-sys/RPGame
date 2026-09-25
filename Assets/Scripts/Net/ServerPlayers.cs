using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The server's players (online phase 2, Docs/KeHoach-Online.md): who is logged in as which
    /// character, their hero, and their saves. A player who logs in gets their character back where
    /// they left it (a new one starts at the zone's spawn); the server sends them their character
    /// sheet and keeps it up to date, and saves it on disk every 30 seconds when something changed,
    /// soon after a level or a big kill, when they leave and when the server stops. It also runs
    /// what players ask for: skills, potions, talking, dialogue commands, stat points and (game
    /// masters only) the console; chat, parties and friends are in ServerPlayers.Social.cs.
    /// </summary>
    [DefaultExecutionOrder(-55)]
    public partial class ServerPlayers : MonoBehaviour
    {
        public static ServerPlayers I { get; private set; }

        /// <summary>Most players in the world at once.</summary>
        public static int MaxPlayers = 20;
        /// <summary>Seconds between saves of a character that changed.</summary>
        public const float SaveEvery = 30f;
        /// <summary>How close to an NPC a hero must be for the server to let them talk.</summary>
        const float TalkReach = 3.5f;

        public ServerStore Store { get; private set; }

        public class Session
        {
            public NetworkConnection conn;
            public AccountRecord account;
            public bool gm;
            public SaveFile file;
            public PlayerController hero;
            public bool ready;
            public float joinedAt;
            public float playTimeBefore;
            public bool unsaved;
            public float lastSaved;
            public float saveAt = -1f;
            /// <summary>The player's ping as their machine reports it (<see cref="LagCompensation"/>).</summary>
            public int pingMs;
            /// <summary>This server's hold on the character while they play (other channels wait for it: <see cref="ServerStore.TryLock"/>).</summary>
            public IDisposable claim;
            /// <summary>Whether the moves their machine reports could have happened (a remote hero only).</summary>
            public readonly MoveCheck moves = new MoveCheck();
            public bool movesFresh = true;
            /// <summary>Until then the server just moved the hero (arrival, getting up, a teleport) and its player's machine catches up: no checks.</summary>
            public float movesQuietUntil;
            public int movesPutBack;
            public readonly Dictionary<string, string> extra = new Dictionary<string, string>();
            public readonly HashSet<string> dirty = new HashSet<string>();
            public string Name => account.name;
            public bool Local => conn != null && conn.IsLocalClient;
        }

        readonly Dictionary<int, Session> sessions = new Dictionary<int, Session>();
        static readonly List<NetworkConnection> Ready = new List<NetworkConnection>();
        NetworkManager nm;
        AccountAuthenticator auth;
        float nextFlush, nextSaveCheck, nextMoveCheck;
        ServerStatus status;
        /// <summary>Seconds between two looks at where remote heroes are (<see cref="MoveCheck"/>).</summary>
        const float MoveCheckEvery = 0.2f;
        DateTime backupDay;

        public IEnumerable<Session> Sessions => sessions.Values;
        public int Count => sessions.Count;

        void Awake() => I = this;

        void OnDestroy()
        {
            SaveAll("server stopping");
            foreach (var s in sessions.Values) s.claim?.Dispose();
            status?.Dispose();
            LagCompensation.Clear();
            End();
            if (I == this) I = null;
            Ready.Clear();
        }

        void OnApplicationQuit() => SaveAll("server stopping");

        // ================================================================== session
        public void Begin(NetworkManager manager, AccountAuthenticator authenticator, ServerStore store)
        {
            nm = manager;
            auth = authenticator;
            Store = store;
            auth.Store = store;
            auth.Refuse = Refuse;
            auth.Claim = name => store.TryLock(name, OnlineSession.Channel);
            auth.Admitted += OnAdmitted;
            nm.ServerManager.OnRemoteConnectionState += OnRemoteState;
            nm.SceneManager.OnClientLoadedStartScenes += OnClientLoaded;
            nm.ServerManager.RegisterBroadcast<CastRequest>(OnCast);
            nm.ServerManager.RegisterBroadcast<ActRequest>(OnAct);
            nm.ServerManager.RegisterBroadcast<ChatRequest>(OnChat);
            GameEvents.EnemyKilled += OnKilled;
            GameEvents.Damaged += OnHeroPushed;
            if (GameSession.Mode == SessionMode.Server) status = new ServerStatus(store.Root, OnlineSession.Channel);
            BackupNow();
            Debug.Log($"[Server] data in {store.Root}: {store.CountAccounts()} characters");
        }

        void End()
        {
            if (nm == null) return;
            if (auth != null) auth.Admitted -= OnAdmitted;
            if (nm.ServerManager != null)
            {
                nm.ServerManager.OnRemoteConnectionState -= OnRemoteState;
                nm.ServerManager.UnregisterBroadcast<CastRequest>(OnCast);
                nm.ServerManager.UnregisterBroadcast<ActRequest>(OnAct);
                nm.ServerManager.UnregisterBroadcast<ChatRequest>(OnChat);
            }
            if (nm.SceneManager != null) nm.SceneManager.OnClientLoadedStartScenes -= OnClientLoaded;
            GameEvents.EnemyKilled -= OnKilled;
            GameEvents.Damaged -= OnHeroPushed;
            nm = null;
        }

        string Refuse(string name)
        {
            string key = LoginCrypto.NameKey(name);
            foreach (var s in sessions.Values)
                if (LoginCrypto.NameKey(s.Name) == key) return $"Nhân vật \"{s.Name}\" đang ở trong game.";
            if (sessions.Count >= MaxPlayers) return $"Máy chủ đã đủ {MaxPlayers} người. Hãy thử lại sau.";
            return null;
        }

        void OnAdmitted(NetworkConnection conn, AccountRecord account, bool created, IDisposable claim)
        {
            var s = new Session
            {
                conn = conn,
                account = account,
                claim = claim,
                gm = Store.IsGm(account.name),
                file = created ? null : Store.LoadCharacter(account.name),
                joinedAt = Time.unscaledTime,
                lastSaved = Time.unscaledTime
            };
            if (s.file != null) s.playTimeBefore = s.file.playTime;
            sessions[conn.ClientId] = s;
            ServerDiscovery.PlayerCount = sessions.Count;
            Debug.Log($"[Server] \"{account.name}\" logged in (player {conn.ClientId}{(created ? ", new character" : "")}{(s.gm ? ", GM" : "")})");
        }

        void OnRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped) return;
            // before FishNet despawns the hero: this is the last look at it
            if (!sessions.TryGetValue(conn.ClientId, out var s)) return;
            Save(s, "left");
            s.claim?.Dispose();   // saved: another channel may have them now
            s.claim = null;
            if (Parties.Of(s.Name) != null) PartyLeave(s, $"{s.Name} đã rời thế giới.");
            Ready.Remove(conn);
            sessions.Remove(conn.ClientId);
            ServerDiscovery.PlayerCount = sessions.Count;
            Debug.Log($"[Server] \"{s.Name}\" left");
            SystemChat($"{s.Name} đã rời thế giới.");
        }

        /// <summary>A player finished loading the world: their hero, with their character, where they left it.</summary>
        void OnClientLoaded(NetworkConnection conn, bool asServer)
        {
            if (!asServer || !sessions.TryGetValue(conn.ClientId, out var s) || s.hero != null) return;
            var gm = GameManager.I;
            var prefab = gm.db.netHeroPrefab.GetComponent<NetworkObject>();
            Vector3 at = SpawnPoint();
            if (s.file != null && TrySavedPosition(s.file, out var saved)) at = saved;
            var nob = nm.GetPooledInstantiated(prefab, at, Quaternion.identity, true);
            var hero = nob.GetComponent<PlayerController>();
            // the character first, so everyone sees their level and health from the start
            if (s.file != null)
            {
                SaveManager.RestoreCharacter(hero, s.file.sections);
                string dialogue = s.file.Get("dialogue");
                if (dialogue != null) s.extra["dialogue"] = dialogue;
            }
            hero.motor.Teleport(at);
            nm.ServerManager.Spawn(nob, conn);
            nm.SceneManager.AddOwnerToDefaultScene(nob);
            s.hero = hero;
            s.movesQuietUntil = Time.unscaledTime + 2f;   // its player's machine takes it from here
            Watch(s);
            NetWorld.AnnounceHero(hero, s.Name);
            if (s.Local)
            {
                // the host plays this one: its conversations' variables are restored here
                if (s.extra.TryGetValue("dialogue", out var json) && DialogueDirector.I != null) DialogueDirector.I.RestoreState(json);
            }
            else
            {
                NetWorld.I.SendEverything(conn);
                foreach (var key in SectionKeys) SendSection(s, key);
                if (s.extra.TryGetValue("dialogue", out var json)) nm.ServerManager.Broadcast(conn, new SectionMsg { key = "dialogue", json = json });
                nm.ServerManager.Broadcast(conn, new ControlMsg { kind = ControlKind.Ready, text = s.Name, ok = s.gm, value = OnlineSession.Channel });
                Ready.Add(conn);
            }
            s.ready = true;
            if (s.file == null) Save(s, "new character");
            Debug.Log($"[Server] hero of \"{s.Name}\" in the world at ({at.x:0.0}, {at.y:0.0})");
            SystemChat($"{s.Name} đã vào thế giới.");
        }

        Vector3 SpawnPoint()
        {
            var spawn = GameManager.I.respawnPoint;
            Vector3 at = spawn != null ? spawn.position : Vector3.zero;
            return at + (Vector3)(UnityEngine.Random.insideUnitCircle * 1.2f);   // not everyone on one spot
        }

        /// <summary>Where a saved character stood, when that is inside the zone.</summary>
        static bool TrySavedPosition(SaveFile f, out Vector3 at)
        {
            at = Vector3.zero;
            string json = f.Get("player");
            if (string.IsNullOrEmpty(json)) return false;
            var p = JsonUtility.FromJson<SavedSpot>(json);
            var zone = ZoneRoot.Current;
            if (p == null || zone == null || !zone.bounds.Contains(new Vector2(p.x, p.y))) return false;
            at = new Vector3(p.x, p.y, 0f);
            return true;
        }

        [Serializable]
        class SavedSpot
        {
            public float x, y;
        }

        // ================================================================== the character sheet
        static readonly string[] SectionKeys = { "player", "inventory", "quests", "bestiary", "waystones" };

        /// <summary>Marks what changed about a hero: its player gets the new sheet, the disk gets it later.</summary>
        void Watch(Session s)
        {
            var h = s.hero;
            if (h.inventory != null) h.inventory.Changed += () => Changed(s, "inventory");
            if (h.stats != null)
            {
                h.stats.Changed += () => Changed(s, "player");
                h.stats.LevelledUp += level => SaveSoon(s);
                // a new look: saved soon, and every screen draws it
                h.stats.LookChanged += () =>
                {
                    Changed(s, "player");
                    SaveSoon(s);
                    NetWorld.AnnounceHero(h, s.Name);
                };
            }
            if (h.quests != null) h.quests.Changed += () => Changed(s, "quests");
            if (h.bestiary != null) h.bestiary.Changed += () => Changed(s, "bestiary");
            if (h.waystones != null) h.waystones.Changed += () => Changed(s, "waystones");
        }

        void Changed(Session s, string key)
        {
            s.unsaved = true;
            if (!s.Local) s.dirty.Add(key);
        }

        void SaveSoon(Session s)
        {
            s.unsaved = true;
            if (s.saveAt < 0f) s.saveAt = Time.unscaledTime + 2f;
        }

        void SendSection(Session s, string key)
        {
            if (s.hero == null || s.Local) return;
            ISaveable part = null;
            if (key == "player") part = s.hero;
            else if (key == "inventory") part = s.hero.inventory;
            else if (key == "quests") part = s.hero.quests;
            else if (key == "bestiary") part = s.hero.bestiary;
            else if (key == "waystones") part = s.hero.waystones;
            if (part == null) return;
            nm.ServerManager.Broadcast(s.conn, new SectionMsg { key = key, json = part.CaptureState() });
        }

        /// <summary>Sends what changed right away (before answering a request that depends on it).</summary>
        void Flush(Session s)
        {
            foreach (var key in s.dirty) SendSection(s, key);
            s.dirty.Clear();
        }

        void Update()
        {
            if (nm == null) return;
            LagCompensation.Tick();
            status?.Tick(this);
            float now = Time.unscaledTime;
            if (now >= nextMoveCheck)
            {
                nextMoveCheck = now + MoveCheckEvery;
                CheckMoves(now);
            }
            if (now >= nextFlush)
            {
                nextFlush = now + 0.15f;
                foreach (var s in sessions.Values)
                    if (s.dirty.Count > 0) Flush(s);
            }
            if (now < nextSaveCheck) return;
            nextSaveCheck = now + 1f;
            foreach (var s in sessions.Values)
            {
                if (!s.ready || !s.unsaved) continue;
                if ((s.saveAt >= 0f && now >= s.saveAt) || now - s.lastSaved >= SaveEvery) Save(s, null);
            }
            if (DateTime.Now.Date != backupDay) BackupNow();
        }

        // ================================================================== moves (phase 5)
        /// <summary>Puts back a remote hero whose reported move could not have happened (<see cref="MoveCheck"/>).</summary>
        void CheckMoves(float now)
        {
            if (NetSmoke.Active) return;   // the automated check moves its heroes around on purpose
            foreach (var s in sessions.Values)
            {
                var hero = s.hero;
                if (hero == null || s.Local || !s.ready) continue;
                Vector2 at = hero.transform.position;
                if (hero.IsDead)
                {
                    s.movesFresh = true;   // gets up somewhere else
                    s.movesQuietUntil = now + 1f;
                    continue;
                }
                if (now < s.movesQuietUntil) continue;
                if (s.movesFresh)
                {
                    s.moves.Reset(at);
                    s.movesFresh = false;
                    continue;
                }
                if (s.moves.Check(at, hero.TopWalkSpeed, now, out Vector2 back)) continue;
                s.movesPutBack++;
                Debug.LogWarning($"[Server] \"{s.Name}\" moved {s.moves.Moved:0.0} units in {MoveCheck.Window:0.#} s (at most {s.moves.Allowed:0.0}): put back ({s.movesPutBack} times)");
                Teleport(hero, back);
            }
        }

        void OnHeroPushed(Health h, DamageInfo d, float amount)
        {
            if (d.knockback <= 0f || h == null || h.team != Team.Player) return;
            var s = SessionOf(h.GetComponent<PlayerController>());
            if (s != null) s.moves.Pushed(d.knockback, Time.unscaledTime);
        }

        void OnKilled(KillInfo k)
        {
            if (k.rank < EnemyRank.MiniBoss) return;
            foreach (var s in sessions.Values)
                if (s.hero != null && k.Credits(s.hero)) SaveSoon(s);
        }

        // ================================================================== saving
        /// <summary>Writes a character to disk now. <paramref name="why"/> is logged (null: a routine save).</summary>
        public void Save(Session s, string why)
        {
            if (s == null || s.hero == null || Store == null) return;
            try
            {
                var hero = s.hero;
                var f = new SaveFile
                {
                    playTime = s.playTimeBefore + (Time.unscaledTime - s.joinedAt),
                    level = hero.stats != null ? hero.stats.level : 1,
                    zone = ZoneArea.At(hero.transform.position) is ZoneArea z ? z.zoneName : "",
                    zoneId = ZoneRoot.Current != null && ZoneRoot.Current.def != null ? ZoneRoot.Current.def.id : "",
                    quest = SaveManager.CurrentQuestTitle(hero)
                };
                foreach (var section in SaveManager.CaptureCharacter(hero)) f.Set(section.key, section.json);
                foreach (var kv in s.extra)
                    if (!string.IsNullOrEmpty(kv.Value) && f.Get(kv.Key) == null) f.Set(kv.Key, kv.Value);
                Store.SaveCharacter(s.Name, f);
                s.unsaved = false;
                s.saveAt = -1f;
                s.lastSaved = Time.unscaledTime;
                if (why != null) Debug.Log($"[Server] saved \"{s.Name}\" ({why})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Server] could not save \"{s.Name}\": {e.Message}");
                Debug.LogException(e);
            }
        }

        string lastSaveAll;
        float lastSaveAllAt = -99f;

        /// <summary>Saves every character now (console "save", the server stopping). Asked twice for the same reason at once (a clean stop, then quitting), it saves once.</summary>
        public void SaveAll(string why)
        {
            if (why == lastSaveAll && Time.unscaledTime - lastSaveAllAt < 2f) return;
            lastSaveAll = why;
            lastSaveAllAt = Time.unscaledTime;
            foreach (var s in sessions.Values)
                if (s.ready) Save(s, why);
        }

        void BackupNow()
        {
            backupDay = DateTime.Now.Date;
            try
            {
                string dir = Store.Backup();
                Debug.Log($"[Server] backup: {dir}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Server] backup failed: " + e.Message);
            }
        }

        // ================================================================== what players ask
        Session SessionOf(NetworkConnection conn) =>
            conn != null && sessions.TryGetValue(conn.ClientId, out var s) && s.hero != null ? s : null;

        void OnCast(NetworkConnection conn, CastRequest r, Channel channel)
        {
            var s = SessionOf(conn);
            if (s == null) return;
            string refused = s.hero.skills.ServerCast(r.slot, r.aim, r.origin);
            if (refused != null) nm.ServerManager.Broadcast(conn, new ControlMsg { kind = ControlKind.CastRefused, id = r.slot, text = refused });
            else
            {
                var ability = r.slot < s.hero.skills.slots.Length ? s.hero.skills.slots[r.slot] : null;
                if (ability != null && ability.HasTag(AbilityTags.Movement)) s.moves.Dashed(Time.unscaledTime);
            }
        }

        void OnAct(NetworkConnection conn, ActRequest r, Channel channel)
        {
            var s = SessionOf(conn);
            if (s == null) return;
            var hero = s.hero;
            bool ok = true;
            switch (r.kind)
            {
                case ActKind.Potion:
                    if (!hero.IsDead) hero.UsePotion(r.value);
                    return;
                case ActKind.SpendStat:
                    if (r.value >= 0 && r.value < CoreStats.Count && hero.stats != null) hero.stats.Spend((CoreStat)r.value);
                    return;
                case ActKind.Forge:
                    Forge.Apply(hero, (Forge.Action)r.id, r.value, r.text);
                    return;
                case ActKind.Equip:
                    if (hero.inventory != null && (r.text == null || r.text.Length < 64)) hero.inventory.ApplyEquip(r.text, (EquipSlot)r.value);
                    return;
                case ActKind.UseItem:
                    if (!hero.IsDead && r.text != null && r.text.Length < 64 && GameManager.I != null) hero.UseItem(GameManager.I.db.Item(r.text));
                    return;
                case ActKind.ChooseLook:
                    if (hero.stats != null) CharacterChoice.Apply(hero, HeroLook.FromJson(r.text));
                    return;
                case ActKind.TalkStart:
                {
                    var npc = NPC.All.Find(n => n != null && n.npcId == r.text);
                    ok = npc != null && !hero.IsDead && Vector2.Distance(npc.transform.position, hero.transform.position) <= TalkReach;
                    if (ok && hero.quests != null) hero.quests.NotifyTalkStarted(npc.npcId);
                    break;
                }
                case ActKind.TalkEnd:
                    if (hero.quests != null) hero.quests.NotifyTalkEnded(r.text);
                    return;
                case ActKind.QuestStart:
                    ok = hero.quests != null && hero.quests.StartQuest(r.text);
                    break;
                case ActKind.QuestComplete:
                    ok = hero.quests != null && hero.quests.CompleteQuest(r.text);
                    break;
                case ActKind.SetFlag:
                    ok = FlagAllowed(r.text);
                    if (ok) hero.quests.SetFlag(r.text);
                    else Debug.LogWarning($"[Server] \"{s.Name}\" asked for an unknown flag {r.text}");
                    break;
                case ActKind.GiveItem:
                {
                    // a script's gift: only game masters online (quest rewards come from the quest itself)
                    var item = s.gm && GameManager.I != null ? GameManager.I.db.Item(r.text) : null;
                    ok = item != null && hero.inventory.Add(item, Mathf.Clamp(r.value, 1, 999));
                    if (!s.gm) Debug.LogWarning($"[Server] \"{s.Name}\" asked for item {r.text} from a dialogue; refused");
                    break;
                }
                case ActKind.DialogueVars:
                    if (r.text != null && r.text.Length < 32000)
                    {
                        s.extra["dialogue"] = r.text;
                        s.unsaved = true;
                    }
                    return;
                case ActKind.PartyInvite:
                    PartyInvite(s, r.text);
                    return;
                case ActKind.PartyAnswer:
                    PartyAnswer(s, r.text, r.value != 0);
                    return;
                case ActKind.PartyLeave:
                    PartyLeave(s);
                    return;
                case ActKind.PartyKick:
                    PartyKick(s, r.text);
                    return;
                case ActKind.Friend:
                    Friend(s, r.text, r.value != 0);
                    return;
                case ActKind.FriendList:
                    FriendList(s);
                    return;
                case ActKind.Ping:
                    s.pingMs = Mathf.Clamp(r.value, 0, 2000);
                    return;
                case ActKind.Travel:
                    if (hero.waystones != null && r.text != null && r.text.Length < 64) hero.waystones.Travel(r.text);
                    return;
                case ActKind.Console:
                    RunConsole(s, r.text);
                    return;
                default:
                    return;
            }
            // the conversation waits for this: first what changed, then the answer
            Flush(s);
            nm.ServerManager.Broadcast(conn, new ControlMsg { kind = ControlKind.Ack, id = r.id, ok = ok });
        }

        static bool FlagAllowed(string flag)
        {
            if (string.IsNullOrEmpty(flag) || GameManager.I == null) return false;
            foreach (var q in GameManager.I.db.quests)
            {
                if (q == null) continue;
                if (q.requiredFlags.Contains(flag) || q.setFlags.Contains(flag)) return true;
            }
            return false;
        }

        void RunConsole(Session s, string line)
        {
            void Reply(string text) => nm.ServerManager.Broadcast(s.conn, new ControlMsg { kind = ControlKind.Console, text = text });
            if (!s.gm)
            {
                Reply("Chỉ tài khoản quản trị (GM) mới dùng được lệnh này trong thế giới online.");
                return;
            }
            Debug.Log($"[Server] GM \"{s.Name}\": {line}");
            DebugConsole.RunFor(s.hero, line, Reply);
        }

        // ================================================================== sending
        /// <summary>
        /// How far from their hero a player is sent the world (enemies, hits, effects): past their
        /// screen (about 30 × 17 units) and the minimap's view, so what they see is always fresh,
        /// while a big world does not cost every player the whole map (Docs/KeHoach-Online.md §12).
        /// </summary>
        public const float ViewRadius = 32f;

        /// <summary>The players in the world and where their heroes are (who sees what).</summary>
        public static void Viewers(List<(NetworkConnection conn, Vector2 at)> into)
        {
            into.Clear();
            var sp = I;
            if (sp == null) return;
            foreach (var c in Ready)
            {
                if (c == null || !c.IsActive || !sp.sessions.TryGetValue(c.ClientId, out var s) || s.hero == null) continue;
                into.Add((c, s.hero.transform.position));
            }
        }

        /// <summary>Every player whose hero is within <paramref name="radius"/> of <paramref name="at"/>.</summary>
        public static void SendNear<T>(T msg, Vector2 at, float radius, Channel channel = Channel.Reliable, PlayerController except = null)
            where T : struct, IBroadcast
        {
            var sp = I;
            var nm = sp != null ? sp.nm : null;
            if (nm == null) return;
            float sq = radius * radius;
            foreach (var c in Ready)
            {
                if (c == null || !c.IsActive || !sp.sessions.TryGetValue(c.ClientId, out var s) || s.hero == null) continue;
                if (s.hero == except || ((Vector2)s.hero.transform.position - at).sqrMagnitude > sq) continue;
                nm.ServerManager.Broadcast(c, msg, true, channel);
            }
        }

        /// <summary>Every player who is in the world (not the host's own screen: it already shows everything).</summary>
        public static void SendToAll<T>(T msg, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            var nm = I != null ? I.nm : null;
            if (nm == null) return;
            foreach (var c in Ready)
                if (c != null && c.IsActive) nm.ServerManager.Broadcast(c, msg, true, channel);
        }

        /// <summary>The player of <paramref name="hero"/> (nothing when that is the host's own hero).</summary>
        public static void SendTo<T>(PlayerController hero, T msg) where T : struct, IBroadcast
        {
            var nm = I != null ? I.nm : null;
            var c = ConnectionOf(hero);
            if (nm == null || c == null || !c.IsActive || c.IsLocalClient || !Ready.Contains(c)) return;
            nm.ServerManager.Broadcast(c, msg, true);
        }

        public static void SendToOwnerOf<T>(LootPickup loot, T msg) where T : struct, IBroadcast
        {
            if (loot != null && loot.owner != null) SendTo(loot.owner, msg);
        }

        public static void SendNotice(PlayerController hero, NoticeMsg m) => SendTo(hero, m);

        public static NetworkConnection ConnectionOf(PlayerController hero)
        {
            if (hero == null || I == null) return null;
            foreach (var s in I.sessions.Values)
                if (s.hero == hero) return s.conn;
            return null;
        }

        public static string NameOf(PlayerController hero)
        {
            if (hero == null || I == null) return null;
            foreach (var s in I.sessions.Values)
                if (s.hero == hero) return s.Name;
            return null;
        }

        public static Session SessionOf(PlayerController hero)
        {
            if (hero == null || I == null) return null;
            foreach (var s in I.sessions.Values)
                if (s.hero == hero) return s;
            return null;
        }

        /// <summary>Moves a hero (a GM's tp): its own player's machine moves it, the server's copy follows.</summary>
        public static void Teleport(PlayerController hero, Vector2 to)
        {
            if (hero == null) return;
            hero.motor.Teleport(to);
            var s = SessionOf(hero);
            if (s != null)
            {
                s.movesFresh = true;
                s.movesQuietUntil = Time.unscaledTime + 1f;
            }
            SendTo(hero, new ControlMsg { kind = ControlKind.Teleport, pos = to });
        }
    }
}
