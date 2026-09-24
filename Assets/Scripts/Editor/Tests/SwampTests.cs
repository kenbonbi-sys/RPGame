using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Đầm Lầy Sương Mù in the real world scene (plays it offline, like <see cref="GameSmokeTests"/>):
    /// the region east of the forest, wading, the Đá Truyền Tống, the mud men's split, poison
    /// pools, both swamp bosses' attacks, and a boss's fight showing on an offline screen.
    /// </summary>
    public class SwampTests
    {
        string tempSaves;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempSaves = Path.Combine(Path.GetTempPath(), "rtt_test_saves_swamp");
            if (Directory.Exists(tempSaves)) Directory.Delete(tempSaves, true);
            SaveManager.FolderOverride = tempSaves;
            Health.SpreadRoll = () => 0.5f;
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            yield return new EnterPlayMode();
            yield return GameSmokeTests.Frames(3);
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((SceneLoader.I == null || SceneLoader.I.Busy) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(ZoneRoot.Current, "the world loaded");
            yield return GameSmokeTests.Frames(2);
            if (HUD.I != null && HUD.I.help != null) HUD.I.help.Close();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return new ExitPlayMode();
            SaveManager.FolderOverride = null;
            Health.SpreadRoll = () => Random.value;
            if (Directory.Exists(tempSaves)) Directory.Delete(tempSaves, true);
        }

        /// <summary>A grid corner with open water around it and to its right (nothing solid in the way).</summary>
        static Vector2 OpenWater(ZoneRoot z)
        {
            int rows = z.terrain.Length / z.terrainWidth;
            for (int y = 3; y < rows - 3; y++)
                for (int x = 3; x < z.terrainWidth - 8; x++)
                {
                    bool wet = true;
                    for (int dx = -1; dx <= 5 && wet; dx++)
                        for (int dy = -1; dy <= 1 && wet; dy++)
                            wet = z.CornerHas(x + dx, y + dy, ZoneRoot.Water);
                    if (!wet) continue;
                    var p = new Vector2(x, y);
                    if (Physics2D.OverlapBox(p + new Vector2(2f, 0.2f), new Vector2(5f, 1.2f), 0f, Layers.ObstacleMask | Layers.EnemyMask) != null) continue;
                    return p;
                }
            Assert.Fail("no open water in the world");
            return Vector2.zero;
        }

        /// <summary>How far the hero walks right in <paramref name="seconds"/> starting at <paramref name="from"/>.</summary>
        static IEnumerator Walk(PlayerController hero, Vector2 from, float seconds, float[] distance)
        {
            hero.motor.Teleport(from);
            hero.Autopilot = true;
            hero.SetIntent(new PlayerIntent());
            yield return GameSmokeTests.GameSeconds(0.2f);
            Vector2 start = hero.transform.position;
            hero.SetIntent(new PlayerIntent { move = Vector2.right });
            yield return GameSmokeTests.GameSeconds(seconds);
            hero.SetIntent(new PlayerIntent());
            distance[0] = Vector2.Distance(start, hero.transform.position);
        }

        [UnityTest]
        public IEnumerator TheSwampLiesEastOfTheForest()
        {
            var zone = ZoneRoot.Current;
            Assert.AreEqual(200f, zone.bounds.width, "one seamless map, twice as wide");
            Assert.NotNull(zone.water);
            Assert.NotNull(zone.mud);
            Assert.AreEqual((int)zone.bounds.width + 1, zone.terrainWidth);
            Assert.AreEqual("Đầm Lầy Sương Mù", ZoneArea.At(new Vector2(150f, 32f)).zoneName);
            Assert.AreEqual("Rừng Thì Thầm", ZoneArea.At(new Vector2(50f, 10f)).zoneName);
            Assert.AreEqual("Làng Lá Xanh", ZoneArea.At(GameManager.I.respawnPoint.position).zoneName);
            foreach (var id in new[] { "bear", "toadking", "snake" }) Assert.NotNull(BossBase.Find(id), id);
            Assert.AreEqual(EnemyRank.MiniBoss, BossBase.Find("toadking").rank);
            Assert.AreEqual(EnemyRank.Boss, BossBase.Find("snake").rank);
            foreach (var id in new[] { "toad", "leech", "mudman" })
                Assert.IsTrue(EnemyBase.All.Exists(e => e.enemyId == id), id + " camps");
            Assert.GreaterOrEqual(Waystone.All.Count, 5, "Đá Truyền Tống in the village, the forest and the swamp");
            foreach (var spot in new[] { "outpost", "swamp", "mudfield", "toadpond", "snakelair" })
                Assert.NotNull(zone.SpotOf(spot), spot + " spot for quests");
            Assert.IsTrue(zone.IsWater(OpenWater(zone)));
            Assert.IsFalse(zone.IsWater(GameManager.I.respawnPoint.position), "the village is dry");
            var swamp = ZoneArea.Named("Đầm Lầy Sương Mù");
            Assert.AreEqual("music_swamp", swamp.music);
            Assert.Greater(swamp.mist, 0f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WaterSlowsWadersButNotSwimmers()
        {
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var zone = ZoneRoot.Current;
            var onLand = new float[1];
            var inWater = new float[1];
            yield return Walk(hero, new Vector2(27f, 19.6f), 0.5f, onLand);
            yield return Walk(hero, OpenWater(zone), 0.5f, inWater);
            Assert.Greater(onLand[0], 1.5f, "walked on the road");
            Assert.Less(inWater[0], onLand[0] * 0.75f, "wading is slower");
            Assert.Greater(inWater[0], onLand[0] * 0.4f, "but the hero is not stuck");
            Assert.IsTrue(hero.motor.InWater);
            foreach (var id in new[] { "toad", "leech" })
                Assert.IsFalse(EnemyBase.All.Find(e => e.enemyId == id).motor.slowedByWater, id + " swims");
            Assert.IsTrue(EnemyBase.All.Find(e => e.enemyId == "mudman").motor.slowedByWater, "a mud man wades");
            Assert.IsFalse(BossBase.Find("snake").motor.slowedByWater, "the snake swims");
        }

        [UnityTest]
        public IEnumerator ABossShowsItsFightOnAnOfflineScreen()
        {
            bool shown = false;
            GameEvents.BossEngaged += (h, n, l) => shown = true;
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var bear = BossBase.Find("bear");
            hero.motor.Teleport(bear.Home + Vector2.down * 5f);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!shown && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(shown, "offline too, the intro brings the boss bar and its music");
            Assert.IsTrue(BossBase.FightShown);
        }

        [UnityTest]
        public IEnumerator WaystonesWakeCarryAndRaise()
        {
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var village = Waystone.Find("village");
            var outpost = Waystone.Find("outpost");
            Assert.NotNull(village);
            Assert.NotNull(outpost);
            Assert.IsFalse(hero.waystones.Knows("outpost"), "a new hero has woken none");

            hero.motor.Teleport(village.Arrival);
            yield return GameSmokeTests.GameSeconds(0.5f);
            Assert.IsTrue(hero.waystones.Knows("village"), "walking up to a stone wakes it");
            hero.motor.Teleport(outpost.Arrival);
            yield return GameSmokeTests.GameSeconds(0.5f);
            Assert.IsTrue(hero.waystones.Knows("outpost"));
            Assert.AreEqual("outpost", hero.waystones.Last, "the last stone touched");
            Assert.IsTrue(GameManager.I.RespawnPointFor(hero, out Vector2 rise));
            Assert.Less(Vector2.Distance(rise, outpost.Arrival), 0.01f, "a fallen hero gets up at it");

            Assert.IsFalse(hero.waystones.Travel("snakelair"), "not to a stone still asleep");
            Assert.IsTrue(hero.waystones.Travel("village"), "to a woken one");
            Assert.Less(Vector2.Distance(hero.transform.position, village.Arrival), 0.1f);
            hero.motor.Teleport(new Vector2(40f, 25f));
            yield return GameSmokeTests.GameSeconds(0.3f);
            Assert.IsFalse(hero.waystones.Travel("outpost"), "only from beside a stone");

            string saved = hero.waystones.CaptureState();
            hero.waystones.RestoreState("{\"known\":[],\"last\":\"\"}");
            Assert.IsFalse(hero.waystones.Knows("outpost"));
            hero.waystones.RestoreState(saved);
            Assert.IsTrue(hero.waystones.Knows("outpost"), "kept with the character");
            Assert.AreEqual("village", hero.waystones.Last);
        }

        [UnityTest]
        public IEnumerator AMudManSplitsIntoMudlings()
        {
            var mud = EnemyBase.All.Find(e => e.enemyId == "mudman" && !e.IsDead) as MudManAI;
            Assert.NotNull(mud);
            Assert.NotNull(mud.brood, "its camp keeps its Bùn Con");
            int before = mud.brood.Out;
            mud.health.Kill();
            yield return GameSmokeTests.Frames(2);
            Assert.AreEqual(before + mud.splitInto, mud.brood.Out, "two Bùn Con come out of it");
        }

        [UnityTest]
        public IEnumerator PoisonPoolsHurtWhoeverStandsInThem()
        {
            var hero = Players.Local;
            hero.motor.Teleport(new Vector2(27f, 19.6f));
            yield return GameSmokeTests.GameSeconds(0.2f);
            float hp = hero.health.hp;
            Assert.NotNull(HazardZone.Poison(hero.transform.position, 1.4f, 5f, 1, null, "thử"), "made where the rules run");
            yield return GameSmokeTests.GameSeconds(1.2f);
            Assert.Less(hero.health.hp, hp, "standing in it hurts");
            Assert.Greater(hero.status.PoisonStacks, 0, "and poisons");
            Assert.IsTrue(HazardZone.AnyAt(hero.transform.position));
            yield return GameSmokeTests.GameSeconds(HazardZone.PoolSeconds);
            Assert.IsFalse(HazardZone.AnyAt(hero.transform.position), "it dries up");
        }

        [UnityTest]
        public IEnumerator SwampBossesRunEveryAttack()
        {
            var hero = Players.Local;
            hero.health.invulnerable = true;
            foreach (var id in new[] { "toadking", "snake" })
            {
                var boss = BossBase.Find(id);
                hero.motor.Teleport(boss.Home + new Vector2(-3f, -4f));
                yield return GameSmokeTests.GameSeconds(2.8f);
                Assert.IsTrue(boss.Engaged, id + " woke for the hero");
                foreach (var attack in boss.Attacks)
                {
                    Assert.IsTrue(boss.DebugForce(attack), id + ": " + attack);
                    yield return GameSmokeTests.GameSeconds(attack == "dive" ? 5f : 3.2f);
                }
                Assert.IsFalse(boss.health.invulnerable, id + " is not left under water");
                boss.health.Kill();
                yield return GameSmokeTests.GameSeconds(1.5f);
                Assert.IsTrue(boss.health.IsDead);
            }
        }
    }

    /// <summary>The swamp's rules that need no scene.</summary>
    public class SwampLogicTests
    {
        [Test]
        public void WaterBlendsAcrossATileLikeItsTiles()
        {
            var go = new GameObject("zone");
            var z = go.AddComponent<ZoneRoot>();
            z.bounds = new Rect(0, 0, 4, 4);
            z.terrainWidth = 5;
            z.terrain = new byte[25];
            foreach (var (x, y) in new[] { (1, 1), (2, 1), (1, 2), (2, 2) }) z.terrain[y * 5 + x] = ZoneRoot.Water;
            Assert.IsTrue(z.IsWater(new Vector2(1.5f, 1.5f)), "between four wet corners");
            Assert.IsTrue(z.IsWater(new Vector2(1f, 1f)), "on a wet corner");
            Assert.IsFalse(z.IsWater(new Vector2(0.4f, 1.5f)), "closer to the dry side of the tile");
            Assert.IsTrue(z.IsWater(new Vector2(0.6f, 1.5f)), "closer to the wet side");
            Assert.IsFalse(z.IsWater(new Vector2(3.5f, 3.5f)));
            Assert.IsFalse(z.IsWater(new Vector2(-1f, 2f)), "off the map is dry");
            Assert.AreEqual(ZoneRoot.WaterSpeed, z.SpeedAt(new Vector2(1.5f, 1.5f)));
            Assert.AreEqual(1f, z.SpeedAt(new Vector2(3.5f, 0.5f)));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ZoneAreasCanBeRectangles()
        {
            var go = new GameObject("area");
            var a = go.AddComponent<ZoneArea>();
            go.transform.position = new Vector3(150f, 32f, 0f);
            a.size = new Vector2(98f, 64f);
            Assert.IsTrue(a.Contains(new Vector2(102f, 1f)), "the corner of the box");
            Assert.IsFalse(a.Contains(new Vector2(100f, 32f)));
            a.size = Vector2.zero;
            a.radius = 5f;
            Assert.IsTrue(a.Contains(new Vector2(153f, 35f)));
            Assert.IsFalse(a.Contains(new Vector2(156f, 32f)));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ATongueHitsWhoIsOnItsLine()
        {
            Vector2 a = Vector2.zero, b = new Vector2(7f, 0f);
            Assert.AreEqual(0.5f, EnemyShots.DistanceToSegment(new Vector2(3f, 0.5f), a, b), 1e-4f);
            Assert.AreEqual(1f, EnemyShots.DistanceToSegment(new Vector2(8f, 0f), a, b), 1e-4f, "past the tip");
            Assert.AreEqual(2f, EnemyShots.DistanceToSegment(new Vector2(-2f, 0f), a, b), 1e-4f, "behind the mouth");
        }

        [Test]
        public void AWaystoneLogKeepsWhatWasWoken()
        {
            var go = new GameObject("hero");
            var log = go.AddComponent<WaystoneLog>();
            log.RestoreState("{\"known\":[\"village\",\"outpost\",\"outpost\",\"\"],\"last\":\"outpost\"}");
            Assert.IsTrue(log.Knows("village"));
            Assert.IsTrue(log.Knows("outpost"));
            Assert.AreEqual(2, log.Known.Count, "no duplicates, no blanks");
            Assert.AreEqual("outpost", log.Last);
            var again = go.AddComponent<WaystoneLog>();
            again.RestoreState(log.CaptureState());
            Assert.AreEqual(2, again.Known.Count);
            Assert.AreEqual("outpost", again.Last);
            again.RestoreState("{}");
            Assert.AreEqual(0, again.Known.Count, "an empty section");
            Assert.AreEqual("", again.Last);
            Object.DestroyImmediate(go);
        }
    }
}
