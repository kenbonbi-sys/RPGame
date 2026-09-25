using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Hang Pha Lê in the real world scene (plays it offline, like <see cref="SwampTests"/>): the
    /// cave north of the swamp with its chambers, stones and camps, the solid rock, its own dark
    /// light whatever the hour, bats scattered by light, a spider's web and a golem's stunning slam.
    /// </summary>
    public class CaveTests
    {
        string tempSaves;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempSaves = Path.Combine(Path.GetTempPath(), "rtt_test_saves_cave");
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

        static Vector2 Spot(string id)
        {
            var t = ZoneRoot.Current.SpotOf(id);
            Assert.NotNull(t, id + " spot");
            return t.position;
        }

        static EnemyBase Nearest(string id, Vector2 near)
        {
            EnemyBase best = null;
            foreach (var e in EnemyBase.All)
                if (e.enemyId == id && !e.IsDead && (best == null ||
                    Vector2.Distance(e.transform.position, near) < Vector2.Distance(best.transform.position, near)))
                    best = e;
            return best;
        }

        [UnityTest]
        public IEnumerator TheCaveLiesNorthOfTheSwamp()
        {
            var zone = ZoneRoot.Current;
            Assert.AreEqual(200f, zone.bounds.width);
            Assert.AreEqual(128f, zone.bounds.height, "the map grew north");
            Assert.NotNull(zone.walls, "the cave's rock has its own tilemap");
            var cave = ZoneArea.At(Spot("crystalforest") + new Vector2(0f, -6f));
            Assert.NotNull(cave);
            var region = ZoneArea.Named("Hang Pha Lê");
            Assert.NotNull(region);
            Assert.AreEqual(1f, region.underground, "under the rock");
            Assert.AreEqual("music_cave", region.music);
            foreach (var id in new[] { "cavemouth", "batcave", "mine", "crystalforest", "spidernest", "crystalhall", "queenhall" })
            {
                var at = Spot(id);
                Assert.Greater(at.y, WorldBuilder.LowH, id + " is north of the swamp");
                Assert.IsFalse(zone.IsWall(at), id + " is on the cave floor");
            }
            Assert.IsTrue(zone.IsWall(new Vector2(150f, 120f)), "solid rock between the chambers");
            Assert.IsFalse(zone.IsWall(new Vector2(40f, 100f)), "the steppe now opens west of the cave");
            Assert.IsTrue(zone.IsWall(new Vector2(40f, 65f)), "the southern cliff still separates the steppe from the forest");
            Assert.IsFalse(zone.IsWall(new Vector2(168f, 60f)), "the way up from the swamp is open");
            foreach (var id in new[] { "cuahang", "rungphale", "nhenchua" })
                Assert.NotNull(Waystone.Find(id), id + " stone");
            foreach (var id in new[] { "bat", "spider", "golem" })
                Assert.IsTrue(EnemyBase.All.Exists(e => e.enemyId == id), id + " camps");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheRockIsSolid()
        {
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var zone = ZoneRoot.Current;
            Assert.Greater(zone.walls.GetComponent<CompositeCollider2D>().pathCount, 0, "the scene saved the rock's collider");
            // the tunnel up from the swamp is about four units wide, rock on both sides and nothing in it
            Vector2 start = new Vector2(168f, 66f);
            Assert.IsFalse(zone.IsWall(start));
            hero.motor.Teleport(start);
            hero.Autopilot = true;
            hero.SetIntent(new PlayerIntent());
            yield return GameSmokeTests.GameSeconds(0.2f);
            hero.SetIntent(new PlayerIntent { move = Vector2.right });
            yield return GameSmokeTests.GameSeconds(1.5f);   // far enough to cross the tunnel and five units of rock
            hero.SetIntent(new PlayerIntent());
            Vector2 at = hero.transform.position;
            Assert.Greater(at.x, start.x + 0.4f, "it walked to the side of the tunnel");
            Assert.Less(at.x, start.x + 4f, "and stopped at the rock");
            Assert.IsFalse(zone.IsWall(at), "never inside it");
        }

        [UnityTest]
        public IEnumerator TheCaveIsDarkWhateverTheHour()
        {
            var dn = DayNightCycle.I;
            dn.dayLength = 0f;
            dn.time = 0.5f;   // noon outside
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var lamp = hero.GetComponentInChildren<NightLight>();
            Assert.NotNull(lamp, "the hero carries a little light");
            hero.motor.Teleport(Spot("cavemouth"));
            yield return GameSmokeTests.GameSeconds(4f);
            Assert.Greater(DayNightCycle.Underground, 0.8f, "under the rock");
            Assert.Greater(DayNightCycle.Darkness, 0.8f, "dark at noon");
            Assert.Less(DayNightCycle.NightFactor, 0.1f, "though it is day outside");
            Assert.Greater(lamp.target.intensity, 0.4f, "the hero's own light burns in the dark");
            hero.motor.Teleport(new Vector2(168f, 45f));
            yield return GameSmokeTests.GameSeconds(4f);
            Assert.Less(DayNightCycle.Underground, 0.2f, "back out in the swamp's daylight");
        }

        [UnityTest]
        public IEnumerator BatsScatterFromLight()
        {
            Assert.IsTrue(BatAI.Bright(DamageType.Fire));
            Assert.IsTrue(BatAI.Bright(DamageType.Holy));
            Assert.IsFalse(BatAI.Bright(DamageType.Physical));
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var bat = Nearest("bat", Spot("batcave")) as BatAI;
            Assert.NotNull(bat);
            Assert.IsTrue(bat.GetComponent<Collider2D>().isTrigger, "it flies");
            hero.motor.Teleport((Vector2)bat.transform.position + new Vector2(-2f, 0f));
            yield return GameSmokeTests.GameSeconds(1f);   // it wakes
            float before = Vector2.Distance(bat.transform.position, hero.transform.position);
            var d = DamageInfo.Make(5f, Team.Player, hero.gameObject, bat.transform.position, Vector2.right, DamageType.Fire);
            bat.health.TakeDamage(d);
            yield return GameSmokeTests.GameSeconds(0.9f);
            Assert.Greater(Vector2.Distance(bat.transform.position, hero.transform.position), before + 1.5f, "a bright hit sends it flapping away");
        }

        [UnityTest]
        public IEnumerator ASpiderWebBinds()
        {
            var hero = Players.Local;
            var spider = Nearest("spider", Spot("spidernest")) as CaveSpiderAI;
            Assert.NotNull(spider);
            Vector2 s = spider.transform.position;
            Vector2 spot = s + Vector2.left * 3.5f;
            foreach (var dir in new[] { Vector2.left, Vector2.right, Vector2.down, Vector2.up })
            {
                var p = s + dir * 3.5f;
                if (!Util.LineBlocked(s, p) && !ZoneRoot.Current.IsWall(p))
                {
                    spot = p;
                    break;
                }
            }
            hero.motor.Teleport(spot);
            yield return GameSmokeTests.GameSeconds(1f);
            hero.motor.Teleport(spot);
            spider.DebugWeb(hero);
            bool bound = false;
            float until = Time.time + spider.webWindup + 1.5f;
            while (Time.time < until && !bound)
            {
                bound = hero.status.IsRooted;
                yield return null;
            }
            Assert.IsTrue(bound, "the web binds (Trói)");
        }

        [UnityTest]
        public IEnumerator AGolemSlamStuns()
        {
            var hero = Players.Local;
            var golem = Nearest("golem", Spot("mine")) as GolemAI;
            Assert.NotNull(golem);
            Assert.Greater(golem.slamStun, 0f);
            Assert.Greater(golem.health.resistances.physical, 0f, "blades glance off rock");
            hero.motor.Teleport((Vector2)golem.transform.position + new Vector2(-1.6f, 0f));
            bool stunned = false;
            float until = Time.time + 6f;
            while (Time.time < until && !stunned)
            {
                stunned = hero.status.IsStunned;
                yield return null;
            }
            Assert.IsTrue(stunned, "its fists come down and stun");
        }
    }
}
