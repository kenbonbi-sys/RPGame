using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Online play (Docs/KeHoach-Online.md). Boots the real game as a host (server and client in
    /// the editor, logged in as a test character, the server's data in a temp folder) and checks
    /// the hero, the shared enemies and boss, kills, XP and the character saved on the server and
    /// given back on the next login. Separate players over the network are checked with the built
    /// game: Debug/NetSmoke.cs.
    /// </summary>
    public class OnlineSessionTests
    {
        /// <summary>A port nothing else uses, so a test never meets a running game.</summary>
        const ushort TestPort = 7791;
        const string TestName = "Thử Nghiệm";
        const string TestPassword = "matkhau";

        static string DataFolder => Path.Combine(Path.GetTempPath(), "rtt_test_server");

        static void Login()
        {
            LoginInfo.Name = TestName;
            LoginInfo.Password = TestPassword;
            LoginInfo.Mode = LoginMode.LoginOrCreate;
            LoginInfo.Remember = false;
        }

        /// <summary>Opens Core in <paramref name="mode"/>; the test enters Play Mode itself (the runner wants that yield at the top).</summary>
        static void Prepare(SessionMode mode)
        {
            GameSession.Mode = mode;
            OnlineSession.Port = TestPort;
            if (Directory.Exists(DataFolder)) Directory.Delete(DataFolder, true);
            OnlineSession.DataFolder = DataFolder;
            LoginInfo.ResetForTests();
            LoginInfo.FolderOverride = Path.Combine(DataFolder, "client");
            Login();
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
        }

        static IEnumerator WaitForZone()
        {
            yield return GameSmokeTests.Frames(3);
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((SceneLoader.I == null || SceneLoader.I.Busy) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(ZoneRoot.Current, "start zone loaded");
            if (HUD.I != null && HUD.I.help != null) HUD.I.help.Close();
        }

        /// <summary>Waits until the host's own networked hero is in the world.</summary>
        static IEnumerator WaitForHero()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((Players.Local == null || Players.Local.GetComponent<NetworkHero>() == null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.NotNull(Players.Local, "the server spawned a hero for the host's own player");
            yield return GameSmokeTests.Frames(2);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return new ExitPlayMode();
            GameSession.Mode = SessionMode.Offline;
            OnlineSession.Port = OnlineSession.DefaultPort;
            OnlineSession.DataFolder = null;
            LoginInfo.ResetForTests();
            LoginInfo.FolderOverride = null;
            if (Directory.Exists(DataFolder)) Directory.Delete(DataFolder, true);
        }

        [UnityTest]
        public IEnumerator AHostPlaysInASharedWorldWithEnemies()
        {
            Prepare(SessionMode.Host);
            yield return new EnterPlayMode();
            yield return WaitForZone();
            yield return WaitForHero();
            var me = Players.Local;
            var net = me.GetComponent<NetworkHero>();
            Assert.IsTrue(net.IsOwner);
            Assert.IsFalse(me.Puppet, "the owner plays its own hero");
            Assert.AreEqual(1, Players.All.Count, "only the networked hero is in the world");
            Assert.IsFalse(GameManager.I.localPlayer.gameObject.activeSelf, "the Core scene's own hero stays out");
            var nm = OnlineSession.I.Network;
            Assert.IsTrue(nm.ServerManager.Started && nm.ClientManager.Started, "server and client both run");
            Assert.Less(Vector2.Distance(me.transform.position, GameManager.I.respawnPoint.position), 2f, "a new character starts at the zone's spawn");
            Assert.AreEqual(me.transform, CameraRig.I.target, "the camera follows it");
            Assert.AreEqual(TestName, ServerPlayers.NameOf(me), "logged in as the test character");
            Assert.AreEqual(1, ServerPlayers.I.Count);

            // the world is shared now: enemies and the boss run on the server
            Assert.Greater(EnemyBase.All.Count, 0, "enemies are in the online world");
            foreach (var e in EnemyBase.All)
            {
                var ne = e.GetComponent<NetEntity>();
                Assert.NotNull(ne, e.name + " is shared");
                Assert.GreaterOrEqual(ne.Id, NetProtocol.SceneIdBase);
            }
            Assert.IsTrue(BossBear.All.Exists(b => b.gameObject.activeSelf && b.GetComponent<NetEntity>() != null), "and the boss");
            Assert.IsFalse(SaveManager.I.CanSave(out _), "no local saves online: the server keeps the character");
            Assert.IsFalse(SaveManager.I.Load(1), "nor loading one");
            StringAssert.Contains("Host", OnlineSession.Status());

            me.Autopilot = true;
            float x = me.transform.position.x;
            me.SetIntent(new PlayerIntent { move = Vector2.left });
            yield return GameSmokeTests.GameSeconds(0.4f);
            me.SetIntent(new PlayerIntent());
            Assert.Less(me.transform.position.x, x - 0.5f, "and walks like offline");

            // a kill the hero took part in gives XP and counts for the Bách Khoa Trùm
            var slime = EnemyBase.All.Find(e => e.enemyId == "slime" && !e.IsDead);
            Assert.NotNull(slime, "a Slime Rêu to fight");
            int xpBefore = me.stats.xp + me.stats.level * 100000;
            var hit = DamageInfo.Make(99999f, Team.Player, me.gameObject, slime.transform.position, Vector2.right);
            hit.pure = true;
            slime.health.TakeDamage(hit);
            yield return GameSmokeTests.Frames(2);
            Assert.IsTrue(slime.IsDead, "the server's slime fell");
            Assert.Greater(me.stats.xp + me.stats.level * 100000, xpBefore, "the hero who hit it got the XP");
            Assert.NotNull(me.bestiary.Find("slime"), "and wrote it down");

            // the server writes the character down (level, bag, quests, where it stands)
            ServerPlayers.I.SaveAll("test");
            var saved = ServerPlayers.I.Store.LoadCharacter(TestName);
            Assert.NotNull(saved, "the character is on the server's disk");
            Assert.AreEqual(me.stats.level, saved.level);
            Assert.NotNull(saved.Get("inventory"));
            Assert.NotNull(saved.Get("quests"));
            Assert.AreEqual(TestName, saved.character);
        }

        [UnityTest]
        public IEnumerator TheServerGivesTheCharacterBackOnTheNextLogin()
        {
            Prepare(SessionMode.Host);
            yield return new EnterPlayMode();
            yield return WaitForZone();
            yield return WaitForHero();
            var me = Players.Local;
            while (me.stats.level < 3) me.stats.AddXp(me.stats.XpToNext - me.stats.xp);
            var gold = me.inventory.gold += 77;
            me.motor.Teleport((Vector2)GameManager.I.respawnPoint.position + new Vector2(3f, 1f));
            Vector2 stood = me.transform.position;
            yield return GameSmokeTests.Frames(3);

            // leave (the server saves) and come back as the same character
            Login();
            OnlineSession.Reboot(SessionMode.Host);
            yield return WaitForZone();
            yield return WaitForHero();
            me = Players.Local;
            Assert.AreEqual(3, me.stats.level, "the level came back");
            Assert.AreEqual(gold, me.inventory.gold, "and the gold");
            Assert.Less(Vector2.Distance(stood, me.transform.position), 0.6f, "and it stands where it left");
        }

        [UnityTest]
        public IEnumerator TheBossFallsForEveryoneWhoHitItAndComesBack()
        {
            Prepare(SessionMode.Host);
            yield return new EnterPlayMode();
            yield return WaitForZone();
            yield return WaitForHero();
            var me = Players.Local;
            var boss = BossBear.All.Find(b => b != null && b.gameObject.activeSelf);
            Assert.NotNull(boss, "the bear is in the online world");
            boss.respawnSeconds = 1f;
            bool barShown = false;
            GameEvents.BossEngaged += (h, n, l) => barShown = true;
            me.health.invulnerable = true;   // the test watches the bear, not the hero
            me.motor.Teleport((Vector2)boss.transform.position + Vector2.down * 4f);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!boss.Engaged && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(boss.Engaged, "a hero walked into the arena: the bear wakes");
            yield return GameSmokeTests.GameSeconds(2.5f);
            Assert.IsTrue(barShown, "this screen's hero is in the fight: its bar shows");

            int before = me.stats.level * 100000 + me.stats.xp;
            var hit = DamageInfo.Make(999999f, Team.Player, me.gameObject, boss.transform.position, Vector2.up);
            hit.pure = true;
            boss.health.TakeDamage(hit);
            Assert.IsTrue(boss.health.IsDead);
            yield return GameSmokeTests.GameSeconds(2f);
            Assert.Greater(me.stats.level * 100000 + me.stats.xp, before, "the hero who hit it gets the boss's XP");
            bool myLoot = false;
            foreach (var l in Object.FindObjectsByType<LootPickup>(FindObjectsInactive.Exclude))
                if (l.gameObject.activeInHierarchy && l.owner == me) myLoot = true;
            Assert.IsTrue(myLoot, "and loot of their own");

            deadline = Time.realtimeSinceStartup + 20f;
            while (!(boss.gameObject.activeSelf && !boss.health.IsDead) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(boss.gameObject.activeSelf && !boss.health.IsDead, "a shared world gets its bear back");
            Assert.AreEqual(boss.maxHp, boss.health.hp, "at full health");
        }

        [UnityTest]
        public IEnumerator APuppetFollowsItsPositionAndAnimates()
        {
            Prepare(SessionMode.Offline);
            yield return new EnterPlayMode();
            yield return WaitForZone();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFactory.CharFolder + "/Player.prefab");
            var other = Object.Instantiate(prefab, new Vector3(500f, 500f), Quaternion.identity).GetComponent<PlayerController>();
            yield return GameSmokeTests.Frames(2);
            other.SetPuppet(true);
            Assert.IsFalse(other.motor.enabled, "the network moves a puppet, not physics");

            other.SetIntent(new PlayerIntent { move = Vector2.right });
            yield return GameSmokeTests.Frames(5);
            Assert.AreEqual(500f, other.transform.position.x, 0.01f, "a puppet does nothing on its own");

            // walking speed, whatever the frame rate (what NetworkTransform does every frame)
            float until = Time.realtimeSinceStartup + 0.4f;
            while (Time.realtimeSinceStartup < until)
            {
                other.transform.position += Vector3.right * (4f * Time.deltaTime);
                yield return GameSmokeTests.Frames(1);
            }
            StringAssert.StartsWith("walk", other.anim.Current, "moving: walks");
            yield return GameSmokeTests.GameSeconds(0.4f);
            StringAssert.StartsWith("idle", other.anim.Current, "stopped: idles");

            other.SetPuppet(false);
            Assert.IsTrue(other.motor.enabled, "and plays on its own again");
            Object.Destroy(other.gameObject);
        }
    }

    /// <summary>Choosing the session mode from the command line.</summary>
    public class OnlineCommandLineTests
    {
        SessionMode mode;
        string address;
        ushort port;
        string data;
        int maxPlayers;

        [SetUp]
        public void Remember()
        {
            mode = GameSession.Mode;
            address = OnlineSession.Address;
            port = OnlineSession.Port;
            data = OnlineSession.DataFolder;
            maxPlayers = ServerPlayers.MaxPlayers;
        }

        [TearDown]
        public void Restore()
        {
            GameSession.Mode = mode;
            OnlineSession.Address = address;
            OnlineSession.Port = port;
            OnlineSession.DataFolder = data;
            ServerPlayers.MaxPlayers = maxPlayers;
            LoginInfo.ResetForTests();
        }

        [Test]
        public void ReadsHostServerAndClient()
        {
            Assert.IsTrue(OnlineSession.Apply(new[] { "RungThiTham.exe", "-host" }));
            Assert.AreEqual(SessionMode.Host, GameSession.Mode);

            Assert.IsTrue(OnlineSession.Apply(new[] { "RungThiTham.exe", "-batchmode", "-server", "-port", "9001", "-data", "D:/rtt", "-maxplayers", "8" }));
            Assert.AreEqual(SessionMode.Server, GameSession.Mode);
            Assert.AreEqual(9001, OnlineSession.Port);
            Assert.AreEqual("D:/rtt", OnlineSession.DataFolder);
            Assert.AreEqual(8, ServerPlayers.MaxPlayers);

            Assert.IsTrue(OnlineSession.Apply(new[] { "RungThiTham.exe", "-client", "192.168.1.5" }));
            Assert.AreEqual(SessionMode.Client, GameSession.Mode);
            Assert.AreEqual("192.168.1.5", OnlineSession.Address);

            Assert.IsTrue(OnlineSession.Apply(new[] { "RungThiTham.exe", "-client", "-netsmoke" }));
            Assert.AreEqual("localhost", OnlineSession.Address, "no address: this machine");
        }

        [Test]
        public void ReadsALoginFromTheCommandLine()
        {
            Assert.IsTrue(OnlineSession.Apply(new[] { "RungThiTham.exe", "-client", "10.0.0.2", "-login", "Mai", "hoamai", "-register" }));
            Assert.AreEqual("Mai", LoginInfo.Name);
            Assert.AreEqual("hoamai", LoginInfo.Password);
            Assert.AreEqual(LoginMode.LoginOrCreate, LoginInfo.Mode);
            Assert.IsFalse(LoginInfo.Remember, "a command-line login is not remembered");
        }

        [Test]
        public void WithoutAModeNothingChanges()
        {
            GameSession.Mode = SessionMode.Offline;
            Assert.IsFalse(OnlineSession.Apply(new[] { "RungThiTham.exe", "-autoshot" }));
            Assert.AreEqual(SessionMode.Offline, GameSession.Mode);
        }
    }
}
