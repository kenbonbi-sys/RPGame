using System;
using System.Collections;
using System.Text;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPG
{
    /// <summary>
    /// Online play (online phase 1, Docs/KeHoach-Online.md). The session mode is chosen when the
    /// game boots: from the command line (-host, -server, -client &lt;address&gt;, -port &lt;n&gt;) or
    /// from the console (host, join, leave), which boot the Core scene again in that mode.
    /// Offline nothing here runs. Online, the Core scene's own hero stays out: the server spawns a
    /// NetHero for every player once they have connected, each player moves their own hero and
    /// sees the others'. Enemies and the boss are not shared yet (phase 3), so they stay away.
    /// </summary>
    public class OnlineSession : MonoBehaviour
    {
        public const ushort DefaultPort = 7770;

        /// <summary>The server a client joins.</summary>
        public static string Address = "localhost";
        public static ushort Port = DefaultPort;

        public static OnlineSession I { get; private set; }

        /// <summary>The running FishNet manager; null offline.</summary>
        public NetworkManager Network { get; private set; }

        static bool commandLineRead;
        bool stopping;

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
        /// -port n. Returns false (and changes nothing but the port) when no mode is named.
        /// </summary>
        public static bool Apply(string[] args)
        {
            string After(string flag)
            {
                int i = Array.IndexOf(args, flag);
                return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
            }
            if (ushort.TryParse(After("-port"), out ushort port) && port > 0) Port = port;
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
        /// host, join, leave). Offline progress is not saved first: save before going online.
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
            // heroes are placed at the zone's spawn, so the zone comes first
            while (SceneLoader.I == null || SceneLoader.I.Busy) yield return null;
            SceneLoader.I.ZoneReady += OnZoneReady;
            KeepEnemiesAway();
            StartNetwork();
        }

        void StartNetwork()
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || db.networkManagerPrefab == null || db.netHeroPrefab == null)
            {
                Debug.LogError("[Online] GameDatabase has no networkManagerPrefab / netHeroPrefab: run Tools/RPG/Build Everything.");
                return;
            }
            var go = Instantiate(db.networkManagerPrefab);
            go.name = "[Network]";
            Network = go.GetComponent<NetworkManager>();
            Network.ServerManager.SetStartOnHeadless(false);
            Network.TransportManager.Transport.SetPort(Port);
            Network.ClientManager.OnClientConnectionState += OnClientState;
            if (RunsServer)
            {
                Network.ServerManager.OnRemoteConnectionState += OnRemoteState;
                Network.SceneManager.OnClientLoadedStartScenes += OnClientReady;
                Network.ServerManager.StartConnection(Port);
                Debug.Log($"[Online] {GameSession.Mode} listening on port {Port}");
            }
            if (RunsClient)
            {
                string address = GameSession.Mode == SessionMode.Host ? "localhost" : Address;
                Network.ClientManager.StartConnection(address, Port);
                Debug.Log($"[Online] joining {address}:{Port}");
                GameEvents.RaiseLog(GameSession.Mode == SessionMode.Host
                    ? $"Đang mở thế giới online (cổng {Port})…"
                    : $"Đang vào thế giới online {address}:{Port}…", Palette.LogQuest);
            }
        }

        void StopNetwork()
        {
            if (Network == null) return;
            stopping = true;
            if (Network.ClientManager.Started) Network.ClientManager.StopConnection();
            if (Network.ServerManager.Started) Network.ServerManager.StopConnection(true);
            Destroy(Network.gameObject);
            Network = null;
        }

        /// <summary>A player finished connecting: the server gives them a hero at the zone's spawn.</summary>
        void OnClientReady(NetworkConnection conn, bool asServer)
        {
            if (!asServer || Network == null) return;
            var gm = GameManager.I;
            var prefab = gm.db.netHeroPrefab.GetComponent<NetworkObject>();
            var spawn = gm.respawnPoint;
            Vector3 at = spawn != null ? spawn.position : Vector3.zero;
            at += (Vector3)(UnityEngine.Random.insideUnitCircle * 1.2f);   // not everyone on one spot
            var nob = Network.GetPooledInstantiated(prefab, at, Quaternion.identity, true);
            Network.ServerManager.Spawn(nob, conn);
            Network.SceneManager.AddOwnerToDefaultScene(nob);
            Debug.Log($"[Online] hero spawned for player {conn.ClientId}");
        }

        void OnRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            bool joined = args.ConnectionState == RemoteConnectionState.Started;
            Debug.Log($"[Online] player {conn.ClientId} {(joined ? "joined" : "left")}");
            if (conn.IsLocalClient) return;   // the host itself
            GameEvents.RaiseLog(joined ? $"Người chơi {conn.ClientId} đã vào." : $"Người chơi {conn.ClientId} đã rời đi.", Palette.LogInfo);
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
            if (GameSession.Mode == SessionMode.Client)
            {
                GameEvents.RaiseLog("Mất kết nối với máy chủ. Quay về chơi một mình…", new Color(1f, 0.6f, 0.5f));
                StartCoroutine(RebootOffline(3f));
            }
        }

        static IEnumerator RebootOffline(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Reboot(SessionMode.Offline);
        }

        void OnZoneReady(ZoneRoot zone) => KeepEnemiesAway();

        /// <summary>Enemies and the boss are not shared until online phase 3: an online world is peaceful for now.</summary>
        static void KeepEnemiesAway()
        {
            foreach (var s in FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include)) s.enabled = false;
            foreach (var e in EnemyBase.All.ToArray()) e.gameObject.SetActive(false);
            foreach (var b in BossBear.All) b.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ status
        /// <summary>One line per fact, for the console's "net" command.</summary>
        public static string Status()
        {
            var sb = new StringBuilder($"Chế độ: {GameSession.Mode}");
            var nm = I != null ? I.Network : null;
            if (nm == null) return sb.Append(GameSession.Mode == SessionMode.Offline ? " (chơi một mình)" : " (chưa chạy)").ToString();
            if (nm.ServerManager.Started) sb.Append($"\nMáy chủ: cổng {Port}, {nm.ServerManager.Clients.Count} người kết nối");
            if (nm.ClientManager.Started) sb.Append($"\nĐã vào: {(GameSession.Mode == SessionMode.Host ? "localhost" : Address)}:{Port}, ping {nm.TimeManager.RoundTripTime} ms");
            sb.Append($"\nNhân vật trong thế giới: {Players.All.Count}");
            return sb.ToString();
        }
    }
}
