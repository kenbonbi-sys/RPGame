using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Online phase 1 (Docs/KeHoach-Online.md): FishNet sessions. Boots the real game as a host
    /// (server and client in the editor), checks the hero it spawns and what stays offline-only.
    /// Two separate players are checked with the built game: Debug/NetSmoke.cs.
    /// </summary>
    public class OnlineSessionTests
    {
        /// <summary>A port nothing else uses, so a test never meets a running game.</summary>
        const ushort TestPort = 7791;

        /// <summary>Opens Core in <paramref name="mode"/>; the test enters Play Mode itself (the runner wants that yield at the top).</summary>
        static void Prepare(SessionMode mode)
        {
            GameSession.Mode = mode;
            OnlineSession.Port = TestPort;
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return new ExitPlayMode();
            GameSession.Mode = SessionMode.Offline;
            OnlineSession.Port = OnlineSession.DefaultPort;
        }

        [UnityTest]
        public IEnumerator AHostSpawnsItsOwnHeroOverTheNetwork()
        {
            Prepare(SessionMode.Host);
            yield return new EnterPlayMode();
            yield return WaitForZone();
            float deadline = Time.realtimeSinceStartup + 15f;
            while ((Players.Local == null || Players.Local.GetComponent<NetworkHero>() == null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            var me = Players.Local;
            Assert.NotNull(me, "the server spawned a hero for the host's own player");
            var net = me.GetComponent<NetworkHero>();
            Assert.NotNull(net, "the local hero is the networked NetHero");
            Assert.IsTrue(net.IsOwner);
            Assert.IsFalse(me.Puppet, "the owner plays its own hero");
            Assert.AreEqual(1, Players.All.Count, "only the networked hero is in the world");
            Assert.IsFalse(GameManager.I.localPlayer.gameObject.activeSelf, "the Core scene's own hero stays out");
            var nm = OnlineSession.I.Network;
            Assert.IsTrue(nm.ServerManager.Started && nm.ClientManager.Started, "server and client both run");
            Assert.Less(Vector2.Distance(me.transform.position, GameManager.I.respawnPoint.position), 2f, "placed at the zone's spawn");
            Assert.AreEqual(me.transform, CameraRig.I.target, "the camera follows it");

            Assert.AreEqual(0, EnemyBase.All.Count, "enemies are not shared yet: none online");
            Assert.IsTrue(BossBear.All.TrueForAll(b => !b.gameObject.activeSelf), "nor the boss");
            Assert.IsFalse(SaveManager.I.CanSave(out _), "no local saves online");
            Assert.IsFalse(SaveManager.I.Load(1), "nor loading one");
            StringAssert.Contains("Host", OnlineSession.Status());

            me.Autopilot = true;
            float x = me.transform.position.x;
            me.SetIntent(new PlayerIntent { move = Vector2.left });
            yield return GameSmokeTests.GameSeconds(0.4f);
            me.SetIntent(new PlayerIntent());
            Assert.Less(me.transform.position.x, x - 0.5f, "and walks like offline");
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

            for (int i = 0; i < 20; i++)
            {
                other.transform.position += Vector3.right * 0.05f;   // what NetworkTransform does, once a game frame
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

        [SetUp]
        public void Remember()
        {
            mode = GameSession.Mode;
            address = OnlineSession.Address;
            port = OnlineSession.Port;
        }

        [TearDown]
        public void Restore()
        {
            GameSession.Mode = mode;
            OnlineSession.Address = address;
            OnlineSession.Port = port;
        }

        [Test]
        public void ReadsHostServerAndClient()
        {
            Assert.IsTrue(OnlineSession.Apply(new[] { "RungThiTham.exe", "-host" }));
            Assert.AreEqual(SessionMode.Host, GameSession.Mode);

            Assert.IsTrue(OnlineSession.Apply(new[] { "RungThiTham.exe", "-batchmode", "-server", "-port", "9001" }));
            Assert.AreEqual(SessionMode.Server, GameSession.Mode);
            Assert.AreEqual(9001, OnlineSession.Port);

            Assert.IsTrue(OnlineSession.Apply(new[] { "RungThiTham.exe", "-client", "192.168.1.5" }));
            Assert.AreEqual(SessionMode.Client, GameSession.Mode);
            Assert.AreEqual("192.168.1.5", OnlineSession.Address);

            Assert.IsTrue(OnlineSession.Apply(new[] { "RungThiTham.exe", "-client", "-netsmoke" }));
            Assert.AreEqual("localhost", OnlineSession.Address, "no address: this machine");
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
