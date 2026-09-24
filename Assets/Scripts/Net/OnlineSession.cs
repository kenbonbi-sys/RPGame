using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FishNet.Broadcast;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPG
{
    /// <summary>
    /// Online play (Docs/KeHoach-Online.md). The session mode is chosen before the Core scene
    /// boots: on the title screen (a player joins the world), from the command line (-server for
    /// the always-on world, -host, -client &lt;address&gt;, -port &lt;n&gt;, -login &lt;name&gt; &lt;password&gt;)
    /// or from the console (host, join, leave). Offline nothing here runs.
    /// Online, once the zone is loaded, this starts FishNet with the login check
    /// (<see cref="AccountAuthenticator"/>), the shared world (<see cref="NetWorld"/>) and, on a
    /// server, its players and their saves (<see cref="ServerPlayers"/>) and the answer to
    /// players' machines looking for it (<see cref="ServerDiscovery"/>). A player's machine gets
    /// its hero with its saved character, sends what its player does, and goes back to the title
    /// screen (which tries again) when the connection is lost.
    /// </summary>
    public class OnlineSession : MonoBehaviour
    {
        public const ushort DefaultPort = 7770;
        public const string TitleScene = "Title";

        /// <summary>The server a client joins.</summary>
        public static string Address = "localhost";
        public static ushort Port = DefaultPort;
        /// <summary>Server: where accounts and characters are kept (null: next to the game, see <see cref="ServerStore.DefaultRoot"/>).</summary>
        public static string DataFolder;
        /// <summary>Server: the name players see when their game finds it.</summary>
        public static string ServerName = "Rừng Thì Thầm";

        public static OnlineSession I { get; private set; }

        /// <summary>The running FishNet manager; null offline.</summary>
        public NetworkManager Network { get; private set; }

        /// <summary>A player's machine: its hero has arrived with its character.</summary>
        public bool InWorld { get; private set; }

        static bool commandLineRead;
        bool stopping;
        ServerDiscovery discovery;
        readonly List<SectionMsg> pendingSections = new List<SectionMsg>();

        static bool RunsServer => GameSession.Mode == SessionMode.Host || GameSession.Mode == SessionMode.Server;
        static bool RunsClient => GameSession.Mode == SessionMode.Host || GameSession.Mode == SessionMode.Client;

        // ------------------------------------------------------------------ choosing the mode
        /// <summary>Reads -host / -server / -client once, the first time the game boots.</summary>
        public static void ReadCommandLine()
        {
            if (commandLineRead) return;
            commandLineRead = true;
            Apply(Environment.GetCommandLineArgs());
        }

        /// <summary>
        /// Sets the session from command-line words: -server, -host or -client [address], plus
        /// -port n, -data folder, -maxplayers n, -servername "…" and -login name password.
        /// Returns false (and changes nothing but those settings) when no mode is named.
        /// </summary>
        public static bool Apply(string[] args)
        {
            string After(string flag)
            {
                int i = Array.IndexOf(args, flag);
                return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
            }
            if (ushort.TryParse(After("-port"), out ushort port) && port > 0) Port = port;
            if (After("-data") != null) DataFolder = After("-data");
            if (int.TryParse(After("-maxplayers"), out int max) && max > 0) ServerPlayers.MaxPlayers = max;
            if (After("-servername") != null) ServerName = After("-servername");
            LoginInfo.ReadCommandLine(args);
            if (Array.IndexOf(args, "-server") >= 0) GameSession.Mode = SessionMode.Server;
            else if (Array.IndexOf(args, "-host") >= 0) GameSession.Mode = SessionMode.Host;
            else if (Array.IndexOf(args, "-client") >= 0)
            {
                GameSession.Mode = SessionMode.Client;
                Address = After("-client") ?? "localhost";
            }
            else return false;
            return true;
        }

        /// <summary>
        /// Ends the current session and boots the game again in <paramref name="mode"/> (console:
        /// host, join). Offline progress is not saved first: save before going online.
        /// </summary>
        public static void Reboot(SessionMode mode, string address = null, ushort port = 0)
        {
            if (I != null) I.StopNetwork();
            GameSession.Mode = mode;
            if (!string.IsNullOrEmpty(address)) Address = address;
            if (port > 0) Port = port;
            TimeFX.Paused = false;
            SceneManager.LoadScene(ZoneRoot.CoreScene, LoadSceneMode.Single);
        }

        /// <summary>Leaves the online world for the title screen (offline play when the build has none).</summary>
        public static void Leave(string why = null)
        {
            LoginInfo.LastError = why;
            if (I != null) I.StopNetwork();
            GameSession.Mode = SessionMode.Offline;
            TimeFX.Paused = false;
            if (Application.CanStreamedLevelBeLoaded(TitleScene)) SceneManager.LoadScene(TitleScene, LoadSceneMode.Single);
            else SceneManager.LoadScene(ZoneRoot.CoreScene, LoadSceneMode.Single);
        }

#if UNITY_EDITOR
        /// <summary>The editor keeps statics between Play sessions: every Play starts offline unless something asks otherwise.</summary>
        [UnityEditor.InitializeOnLoadMethod]
        static void BackToOfflineAfterPlay()
        {
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) GameSession.Mode = SessionMode.Offline;
            };
        }
#endif

        // ------------------------------------------------------------------ running
        void Awake() => I = this;

        void OnDestroy()
        {
            StopNetwork();
            if (I == this) I = null;
        }

        IEnumerator Start()
        {
            if (GameSession.Mode == SessionMode.Offline) yield break;
            if (GameSession.Mode == SessionMode.Server) Application.targetFrameRate = 60;   // a server has no screen to keep smooth
            // heroes are placed in the zone, so the zone comes first
            while (SceneLoader.I == null || SceneLoader.I.Busy) yield return null;
            var world = gameObject.GetComponent<NetWorld>();
            if (world == null) world = gameObject.AddComponent<NetWorld>();
            yield return world.RegisterZone(ZoneRoot.Current);
            StartNetwork(world);
        }

        void StartNetwork(NetWorld world)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || db.networkManagerPrefab == null || db.netHeroPrefab == null)
            {
                Debug.LogError("[Online] GameDatabase has no networkManagerPrefab / netHeroPrefab: run Tools/RPG/Build Everything.");
                return;
            }
            if (RunsClient && !LoginInfo.Ready) PickHostLogin();
            var go = Instantiate(db.networkManagerPrefab);
            go.name = "[Network]";
            Network = go.GetComponent<NetworkManager>();
            Network.ServerManager.SetStartOnHeadless(false);
            Network.TransportManager.Transport.SetPort(Port);
            var auth = go.AddComponent<AccountAuthenticator>();
            Network.ServerManager.SetAuthenticator(auth);
            if (RunsServer)
            {
                string folder = !string.IsNullOrEmpty(DataFolder) ? DataFolder : ServerStore.DefaultRoot();
                var players = gameObject.GetComponent<ServerPlayers>();
                if (players == null) players = gameObject.AddComponent<ServerPlayers>();
                players.Begin(Network, auth, new ServerStore(folder));
                Network.ServerManager.StartConnection(Port);
                discovery = ServerDiscovery.StartResponder(Port, () => ServerName, () => ServerPlayers.I != null ? ServerPlayers.I.Count : 0, ServerPlayers.MaxPlayers);
                Debug.Log($"[Online] {GameSession.Mode} \"{ServerName}\" listening on port {Port}, up to {ServerPlayers.MaxPlayers} players");
            }
            world.Begin(Network);
            if (RunsClient)
            {
                var c = Network.ClientManager;
                c.OnClientConnectionState += OnClientState;
                c.RegisterBroadcast<SectionMsg>(OnSection);
                c.RegisterBroadcast<NoticeMsg>(OnNotice);
                c.RegisterBroadcast<ControlMsg>(OnControl);
                string address = GameSession.Mode == SessionMode.Host ? "localhost" : Address;
                c.StartConnection(address, Port);
                Debug.Log($"[Online] joining {address}:{Port} as \"{LoginInfo.Name}\"");
                if (GameSession.Mode == SessionMode.Client) GameEvents.RaiseLog($"Đang vào thế giới online {address}…", Palette.LogQuest);
            }
        }

        /// <summary>A host with nobody logged in plays as "Chủ Phòng", with a password only this machine knows.</summary>
        static void PickHostLogin()
        {
            if (LoginInfo.UseRemembered()) return;
            const string key = "rtt.hostSecret";
            string secret = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(secret))
            {
                secret = Convert.ToBase64String(LoginCrypto.RandomBytes(18));
                PlayerPrefs.SetString(key, secret);
                PlayerPrefs.Save();
            }
            LoginInfo.Name = "Chủ Phòng";
            LoginInfo.Password = secret;
            LoginInfo.Mode = LoginMode.LoginOrCreate;
            LoginInfo.Remember = false;
        }

        void StopNetwork()
        {
            if (discovery != null)
            {
                discovery.Dispose();
                discovery = null;
            }
            if (Network == null) return;
            stopping = true;
            var world = GetComponent<NetWorld>();
            if (world != null) world.End();
            if (ServerPlayers.I != null) ServerPlayers.I.SaveAll("session ends");
            if (Network.ClientManager.Started) Network.ClientManager.StopConnection();
            if (Network.ServerManager.Started) Network.ServerManager.StopConnection(true);
            Destroy(Network.gameObject);
            Network = null;
            NetCues.Reset();
        }

        void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                Debug.Log("[Online] connected");
                return;
            }
            if (args.ConnectionState != LocalConnectionState.Stopped || stopping) return;
            Debug.Log("[Online] disconnected");
            if (GameSession.Mode != SessionMode.Client) return;
            var r = LoginInfo.LastResult;
            string why;
            if (r.HasValue && r.Value.code != LoginCode.Ok) why = r.Value.message;
            else if (InWorld) why = "Mất kết nối với máy chủ.";
            else why = "Không vào được máy chủ.";
            GameEvents.RaiseLog(why + " Về màn hình chính…", new Color(1f, 0.6f, 0.5f));
            StartCoroutine(LeaveSoon(why, InWorld ? 1.5f : 0.2f));
        }

        IEnumerator LeaveSoon(string why, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Leave(why);
        }

        // ------------------------------------------------------------------ this player's own messages
        void OnSection(SectionMsg m, Channel channel)
        {
            var me = Players.Local;
            if (me == null)
            {
                pendingSections.Add(m);   // the hero is on its way
                return;
            }
            ApplySection(me, m);
        }

        /// <summary>The hero of this screen arrived: the character sheet that came before it.</summary>
        public void HeroArrived(PlayerController me)
        {
            foreach (var m in pendingSections) ApplySection(me, m);
            pendingSections.Clear();
        }

        static void ApplySection(PlayerController me, SectionMsg m)
        {
            try
            {
                switch (m.key)
                {
                    case "player": me.RestoreProgress(m.json); break;
                    case "inventory": if (me.inventory != null) me.inventory.RestoreState(m.json); break;
                    case "quests": if (me.quests != null) me.quests.RestoreState(m.json); break;
                    case "bestiary": if (me.bestiary != null) me.bestiary.RestoreState(m.json); break;
                    case "dialogue": if (DialogueDirector.I != null) DialogueDirector.I.RestoreState(m.json); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void OnNotice(NoticeMsg m, Channel channel) => Notify.Show(m);

        void OnControl(ControlMsg m, Channel channel)
        {
            var me = Players.Local;
            switch (m.kind)
            {
                case ControlKind.Ready:
                    InWorld = true;
                    GameEvents.RaiseLog($"Chào {m.text}! Bạn đã vào thế giới online.{(m.ok ? " (Quản trị)" : "")}", Palette.LogQuest);
                    break;
                case ControlKind.Downed:
                    if (me == null) break;
                    me.health.SetRemoteDead(true);
                    if (GameManager.I != null) GameManager.I.LocalDowned(m.value);
                    break;
                case ControlKind.Respawn:
                    if (me == null) break;
                    me.Respawn(m.pos);
                    if (GameManager.I != null) GameManager.I.LocalRespawned();
                    break;
                case ControlKind.Teleport:
                    if (me == null) break;
                    me.motor.Teleport(m.pos);
                    if (CameraRig.I != null) CameraRig.I.SnapToTarget();
                    break;
                case ControlKind.CastRefused:
                    if (me != null) me.skills.Refused(m.id, m.text);
                    break;
                case ControlKind.Ack:
                    Answers[m.id] = m.ok;
                    break;
                case ControlKind.Console:
                    if (DebugConsole.I != null) DebugConsole.I.Print(m.text);
                    break;
            }
        }

        // ------------------------------------------------------------------ asking the server
        /// <summary>A player's machine sends a request to the server (nothing when not connected).</summary>
        public static void Ask<T>(T msg) where T : struct, IBroadcast
        {
            var nm = I != null ? I.Network : null;
            if (nm == null || !nm.ClientManager.Started) return;
            nm.ClientManager.Broadcast(msg);
        }

        /// <summary>The server's answer to a request that waits (see <see cref="AskAndWait"/>).</summary>
        public class Answer
        {
            public bool done;
            public bool ok;
        }

        static readonly Dictionary<int, bool> Answers = new Dictionary<int, bool>();
        static int nextAsk = 1;

        /// <summary>Sends a request and waits (up to 5 s) for the server's answer; dialogue commands and talking use it.</summary>
        public static IEnumerator AskAndWait(ActRequest request, Answer answer = null)
        {
            int id = nextAsk++;
            request.id = id;
            Ask(request);
            float until = Time.realtimeSinceStartup + 5f;
            while (!Answers.ContainsKey(id) && Time.realtimeSinceStartup < until && I != null && I.Network != null) yield return null;
            bool ok = Answers.TryGetValue(id, out bool got) && got;
            Answers.Remove(id);
            if (answer != null)
            {
                answer.done = true;
                answer.ok = ok;
            }
        }

        /// <summary>Says something to everyone in the world.</summary>
        public static void Say(string text)
        {
            text = ServerPlayers.CleanChat(text);
            if (text.Length == 0) return;
            if (GameSession.Mode == SessionMode.Client)
            {
                Ask(new ActRequest { kind = ActKind.Chat, text = text });
                return;
            }
            if (GameSession.Serving && ServerPlayers.I != null)
            {
                string name = ServerPlayers.NameOf(Players.Local) ?? "Máy chủ";
                var msg = new ChatMsg { from = name, text = text };
                ServerPlayers.SendToAll(msg);
                if (GameSession.HasScreen) NetWorld.ShowChat(msg);
                return;
            }
            GameEvents.RaiseLog("Chưa vào thế giới online.", Palette.LogInfo);
        }

        // ------------------------------------------------------------------ status
        /// <summary>One line per fact, for the console's "net" command.</summary>
        public static string Status()
        {
            var sb = new StringBuilder($"Chế độ: {GameSession.Mode}");
            var nm = I != null ? I.Network : null;
            if (nm == null) return sb.Append(GameSession.Mode == SessionMode.Offline ? " (chơi một mình)" : " (chưa chạy)").ToString();
            if (nm.ServerManager.Started)
                sb.Append($"\nMáy chủ \"{ServerName}\": cổng {Port}, {(ServerPlayers.I != null ? ServerPlayers.I.Count : 0)}/{ServerPlayers.MaxPlayers} người" +
                          $"\nDữ liệu: {(ServerPlayers.I != null && ServerPlayers.I.Store != null ? ServerPlayers.I.Store.Root : "?")}");
            if (nm.ClientManager.Started) sb.Append($"\nĐã vào: {(GameSession.Mode == SessionMode.Host ? "localhost" : Address)}:{Port} là \"{LoginInfo.Name}\", ping {nm.TimeManager.RoundTripTime} ms");
            sb.Append($"\nNhân vật trong thế giới: {Players.All.Count}");
            return sb.ToString();
        }

        /// <summary>Who is in the world (console "players").</summary>
        public static string PlayerList()
        {
            var names = new List<string>();
            if (ServerPlayers.I != null)
                foreach (var s in ServerPlayers.I.Sessions)
                    names.Add(s.gm ? s.Name + " (GM)" : s.Name);
            else
                foreach (var p in Players.All)
                {
                    string n = NetWorld.HeroName(NetWorld.IdOf(p));
                    if (n != null) names.Add(n);
                }
            return names.Count == 0 ? "Chưa có ai." : $"{names.Count} người: " + string.Join(", ", names);
        }
    }
}
