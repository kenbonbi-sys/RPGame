using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Thảo Nguyên Gió in the real world scene played offline (T61): the steppe opens west of the
    /// crystal forest, the wind turns in big swings, pushes walkers and bends shots only there,
    /// Khe Vực stops walkers while the wind columns carry them across, a hyena pack comes together
    /// and lunges, an eagle marks, swoops and sits open, and a bison rams rock.
    /// </summary>
    public class SteppeTests
    {
        string tempSaves;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempSaves = Path.Combine(Path.GetTempPath(), "rtt_test_saves_steppe");
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
            Wind.Override = null;
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

        /// <summary>Waits game seconds (WaitForSeconds does not wait in these tests).</summary>
        static IEnumerator Seconds(float s)
        {
            float end = Time.time + s;
            float deadline = Time.realtimeSinceStartup + s + 10f;
            while (Time.time < end)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "game time must advance while waiting");
                yield return null;
            }
        }

        static int Count(string id) => EnemyBase.All.FindAll(e => e.enemyId == id).Count;

        /// <summary>A spot of open grass near <paramref name="near"/>: nothing in the way for a few steps right and up.</summary>
        static Vector2 OpenSpot(Vector2 near)
        {
            int mask = Layers.ObstacleMask | 1;
            for (int r = 0; r < 12; r++)
                for (int k = 0; k < 12; k++)
                {
                    var p = near + Util.FromAngle(k * 30f) * r * 1.5f;
                    if (ZoneRoot.Current.IsWall(p) || ZoneRoot.Current.IsChasm(p) || Wind.WindinessAt(p) < 1f) continue;
                    if (Physics2D.OverlapCircle(p, 0.6f, mask) != null) continue;
                    if (Physics2D.CircleCast(p, 0.45f, Vector2.right, 3f, mask).collider != null) continue;
                    if (Physics2D.CircleCast(p, 0.45f, Vector2.up, 6f, mask).collider != null) continue;
                    return p;
                }
            Assert.Fail("no open grass near " + near);
            return near;
        }

        static PlayerController Hero()
        {
            var hero = Players.Local;
            hero.stats.look = new HeroLook();
            CharacterChoice.Apply(hero, new HeroLook { cls = "fighter", race = "human", weapon = "sword" });
            return hero;
        }

        [UnityTest]
        public IEnumerator TheSteppeOpensWestOfTheCrystalForest()
        {
            var zone = ZoneRoot.Current;
            var steppe = ZoneArea.Named("Thảo Nguyên Gió");
            Assert.NotNull(steppe, "the region");
            Assert.AreEqual(1f, steppe.windy, "the wind blows there");
            Assert.AreEqual("music_steppe", steppe.music);
            foreach (var id in new[] { "cuagio", "traidumuc", "tayvuc" }) Assert.NotNull(Waystone.Find(id), id);
            // the tunnel from the crystal forest: open all the way
            for (float x = 99f; x <= 124f; x += 0.5f)
            {
                float y = x < 108f ? Mathf.Lerp(91.5f, 92.5f, (x - 98.5f) / 9.5f) : x < 117f ? Mathf.Lerp(92.5f, 93.5f, (x - 108f) / 9f) : Mathf.Lerp(93.5f, 94.5f, (x - 117f) / 8f);
                Assert.IsFalse(zone.IsWall(new Vector2(x, y)), $"the tunnel is open at {x:0.0}");
            }
            Assert.IsFalse(zone.IsWall(Spot("windgate")), "the wind gate");
            Assert.IsFalse(zone.IsWall(Spot("nomadcamp")), "the nomads' camp");
            Assert.IsTrue(NPC.All.Exists(n => n != null && n.npcId == "giatang"), "Già Tăng at the camp");
            Assert.GreaterOrEqual(Count("hyena"), 9, "three hyena packs");
            Assert.GreaterOrEqual(Count("eagle"), 6, "eagles over their rocks");
            Assert.GreaterOrEqual(Count("bison"), 5, "two bison herds");
            Assert.AreEqual(6, WindColumn.All.Count, "a pair of wind columns at each of three crossings");
            foreach (var c in WindColumn.All)
            {
                Assert.NotNull(c.partner, c.columnId);
                Assert.AreEqual(c, c.partner.partner, "partners carry back");
                Assert.Greater(c.ravine.Length, 0, "it knows the ravine");
            }
            var quest = GameManager.I.db.quests.Find(q => q.id == "steppe_enter");
            Assert.NotNull(quest);
            Assert.IsTrue(quest.requires.Exists(r => r != null && r.id == "slay_queen"), "after the spider queen");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheWindTurnsPushesWalkersAndBendsShots()
        {
            for (int k = 0; k < 60; k++)
            {
                float a = Wind.HeadingOf(k), b = Wind.HeadingOf(k + 1);
                Assert.GreaterOrEqual(Mathf.Abs(Mathf.DeltaAngle(a, b)), 60f, "each turn of the wind is a big one");
            }
            Assert.Less(Wind.StrengthAt(0.05f), 0.3f, "a calm");
            Assert.AreEqual(1f, Wind.StrengthAt(0.5f), 0.01f, "a gust");

            var hero = Hero();
            hero.health.invulnerable = true;
            Wind.Override = Vector2.right;
            var camp = OpenSpot(Spot("windgate") + new Vector2(-8f, 4f));
            Assert.Greater(Wind.At(camp).x, 0.9f, "the wind blows on the steppe");
            Assert.AreEqual(Vector2.zero, Wind.At(new Vector2(20f, 20f)), "not in the village");
            hero.motor.Teleport(camp);
            yield return Seconds(0.3f);
            Assert.Greater(hero.motor.WindPush.x, Wind.HeroPush * 0.9f, "the wind pushes the hero");
            yield return Seconds(0.7f);
            float pushed = hero.transform.position.x - camp.x;
            Assert.Greater(pushed, Wind.HeroPush * 0.6f, "pushed along by the wind");
            Assert.Less(pushed, Wind.HeroPush * 1.5f);

            // in the village nobody is pushed
            var village = new Vector2(18.5f, 16f);
            hero.motor.Teleport(village);
            yield return Seconds(0.6f);
            Assert.Less(Vector2.Distance(hero.transform.position, village), 0.1f, "the village has no wind");

            // a shot drifts with the wind
            var db = GameManager.I.db;
            var go = Pool.Get(db.fireballPrefab, camp, Quaternion.identity);
            var shot = go.GetComponent<Projectile>();
            shot.lifetime = 5f;
            shot.rotateToDirection = true;
            shot.Launch(Vector2.up, null, false, false);
            yield return Seconds(0.5f);
            Vector2 at = shot.transform.position;
            Assert.Greater(at.x - camp.x, 0.6f, "blown sideways");
            Assert.Greater(at.y - camp.y, 2f, "still flying where it was shot");
            var straight = Quaternion.Euler(0, 0, Util.Angle(Vector2.up));
            Assert.Greater(Quaternion.Angle(straight, shot.transform.rotation), 5f, "the sprite follows its wind-bent flight");
            Wind.Override = Vector2.zero;
            yield return Seconds(0.08f);
            Assert.Less(Quaternion.Angle(straight, shot.transform.rotation), 0.1f, "when the gust ends the sprite points straight again");
            go.SetActive(false);
        }

        [UnityTest]
        public IEnumerator KheVucStopsWalkersButTheWindColumnsCarry()
        {
            var hero = Hero();
            hero.health.invulnerable = true;
            Wind.Override = Vector2.zero;
            var zone = ZoneRoot.Current;
            var east = WindColumn.All.Find(c => c.columnId == "e96");
            Assert.NotNull(east, "the east column of the middle crossing");
            var west = east.partner;
            Vector2 e = east.transform.position, w = west.transform.position;
            // walk straight into the ravine from beside the east ring
            var start = e + Vector2.down * 2.2f;
            hero.motor.Teleport(start);
            yield return null;
            hero.Autopilot = true;
            hero.SetIntent(new PlayerIntent { move = Vector2.left });
            yield return Seconds(2.5f);
            hero.SetIntent(new PlayerIntent());
            Assert.Greater(hero.transform.position.x, w.x + 2f, "Khe Vực stops a walker");
            Assert.IsFalse(zone.IsChasm(hero.transform.position), "nobody stands over the drop");
            // step into the column: carried across
            hero.motor.Teleport(e);
            float deadline = Time.time + 4f;
            while (Time.time < deadline && hero.transform.position.x > w.x + 1f) yield return null;
            while (WindColumn.Carrying && Time.time < deadline + 2f) yield return null;
            Assert.Less(hero.transform.position.x, w.x + 0.5f, "on the far rim");
            Assert.IsFalse(zone.IsChasm(hero.transform.position));
            Assert.Greater(Vector2.Distance(hero.transform.position, w), west.radius, "landed past the ring, not in it");
            hero.Autopilot = false;
        }

        [UnityTest]
        public IEnumerator AHyenaPackHuntsTogether()
        {
            var hero = Hero();
            hero.health.invulnerable = false;
            hero.health.hp = hero.health.maxHp = 5000f;
            Wind.Override = Vector2.zero;
            var den = Spot("hyenas");
            var h = Nearest("hyena", den) as HyenaAI;
            Assert.NotNull(h);
            hero.motor.Teleport((Vector2)h.transform.position + Vector2.right * 3f);
            yield return Seconds(0.3f);
            float before = hero.health.hp;
            h.DebugLunge(hero);
            float until = Time.time + h.lungeWindup + h.lungeSeconds + 1f;
            while (Time.time < until && hero.health.hp >= before) yield return null;
            Assert.Less(hero.health.hp, before, "the lunge bites");
            yield return Seconds(0.5f);
            int hunting = EnemyBase.All.FindAll(e => e is HyenaAI && e.Busy && Vector2.Distance(e.transform.position, h.transform.position) < 12f).Count;
            Assert.GreaterOrEqual(hunting, 2, "the pack came");
        }

        [UnityTest]
        public IEnumerator InterruptingAColumnRestoresTheHeroAndRavineCollision()
        {
            var hero = Hero();
            hero.health.invulnerable = true;
            Wind.Override = Vector2.zero;
            var east = WindColumn.All.Find(c => c.columnId == "e96");
            Assert.NotNull(east);
            var body = hero.anim.transform;
            Vector3 rest = body.localPosition;
            hero.motor.Teleport(east.transform.position);
            yield return Seconds(0.5f);
            Assert.IsTrue(WindColumn.Carrying, "flight started");
            Assert.Greater(body.localPosition.y, rest.y + 0.5f, "the hero rose above the shadow");
            var own = hero.GetComponent<Collider2D>();
            Assert.IsTrue(Physics2D.GetIgnoreCollision(own, east.ravine[0]), "the flight passes over the ravine");
            east.gameObject.SetActive(false);
            yield return Seconds(0.1f);
            Assert.IsFalse(WindColumn.Carrying, "an interrupted flight must release the next column");
            Assert.AreEqual(rest, body.localPosition, "the body returned to its usual height");
            Assert.IsFalse(Physics2D.GetIgnoreCollision(own, east.ravine[0]), "walking collisions restored");
            Assert.IsFalse(ZoneRoot.Current.IsChasm(hero.transform.position), "safe on the departure bank");
            east.gameObject.SetActive(true);
            hero.motor.Teleport(east.transform.position);
            yield return Seconds(2.5f);
            Assert.IsFalse(WindColumn.Carrying);
            Assert.Less(Vector2.Distance(hero.transform.position, east.partner.Landing), 0.35f, "another flight still works");
        }

        /// <summary>
        /// Online, another player's hero is flown by its own machine; this screen only sees the network
        /// glide it over the ravine and must draw the arc itself, then let it down on the far rim.
        /// </summary>
        [UnityTest]
        public IEnumerator AnotherPlayersFlightIsDrawnOnThisScreen()
        {
            var hero = Hero();
            hero.health.invulnerable = true;
            Wind.Override = Vector2.zero;
            var east = WindColumn.All.Find(c => c.columnId == "e96");
            Assert.NotNull(east);
            var body = hero.anim.transform;
            Vector3 rest = body.localPosition;
            var rb = hero.GetComponent<Rigidbody2D>();
            // what another player's hero is on this screen: a kinematic puppet the network moves
            hero.SetPuppet(true);
            Players.SetLocal(null);
            rb.bodyType = RigidbodyType2D.Kinematic;
            try
            {
                Vector2 from = (Vector2)east.transform.position + Vector2.right * 0.9f, to = east.partner.Landing;
                float seconds = Vector2.Distance(from, to) / WindColumn.CarrySpeed;
                float start = Time.time, top = 0f;
                while (true)
                {
                    float k = Mathf.Clamp01((Time.time - start) / seconds);
                    Vector2 p = Vector2.Lerp(from, to, k);
                    rb.position = p;
                    hero.transform.position = p;
                    yield return null;
                    top = Mathf.Max(top, body.localPosition.y - rest.y);
                    if (k >= 1f) break;
                }
                yield return Seconds(0.2f);
                Assert.Greater(top, WindColumn.CarryHeight * 0.7f, "rose above its shadow over the ravine");
                Assert.AreEqual(rest.y, body.localPosition.y, 0.01f, "down again on the far rim");
                Assert.IsFalse(WindColumn.Carrying, "this screen's own hero is not the one flying");

                // one that stands in a ring without flying (it dashed in) is let go, not lifted
                rb.position = east.transform.position;
                hero.transform.position = east.transform.position;
                yield return Seconds(1.2f);
                Assert.AreEqual(rest.y, body.localPosition.y, 0.01f, "standing in the ring is not a flight");
            }
            finally
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                hero.SetPuppet(false);
                Players.SetLocal(hero);
            }
        }

        [UnityTest]
        public IEnumerator AnEagleMarksSwoopsAndSitsOpen()
        {
            var hero = Hero();
            hero.health.hp = hero.health.maxHp = 5000f;
            Wind.Override = Vector2.zero;
            var eagle = Nearest("eagle", Spot("eagles")) as EagleAI;
            Assert.NotNull(eagle);
            // Another eagle's strike or a nearby hyena must not satisfy this assertion,
            // or knock the hero out of this eagle's mark before it lands.
            foreach (var other in EnemyBase.All.ToArray())
                if (other != eagle) other.gameObject.SetActive(false);
            hero.motor.Teleport((Vector2)eagle.transform.position + new Vector2(2f, -1f));
            yield return Seconds(0.3f);
            bool struck = false;
            hero.health.Damaged += (hit, amount) =>
            {
                if (hit.source == eagle.gameObject && hit.skillName == "Bổ Nhào" && amount > 0f) struck = true;
            };
            eagle.DebugSwoop(hero);
            Assert.Greater(eagle.lift.localPosition.y, 1.5f, "high above its shadow");
            float until = Time.time + eagle.markSeconds + eagle.diveSeconds + 0.6f;
            // Grounded also includes the low final part of the dive, before the talons hit.
            while (Time.time < until && !struck) yield return null;
            Assert.IsTrue(struck, "this eagle struck where it marked");
            Assert.IsTrue(eagle.Grounded, "then it sits on the ground");
            Assert.Less(eagle.lift.localPosition.y, 0.5f);
            var d = DamageInfo.Make(100f, Team.Player, null, eagle.transform.position, Vector2.down, DamageType.Physical, 0f);
            Assert.AreEqual(eagle.groundedBonus, eagle.health.Guard(d), 0.001f, "open to blades on the ground");
        }

        [UnityTest]
        public IEnumerator ABisonRamsRockAndIsDazed()
        {
            var hero = Hero();
            hero.health.invulnerable = true;
            Wind.Override = Vector2.zero;
            var bison = Nearest("bison", Spot("bisons")) as StoneBeetleAI;
            Assert.NotNull(bison);
            Assert.AreEqual("Lao Húc", bison.chargeName);
            yield return Seconds(0.3f);
            Vector2 b = bison.transform.position;
            Vector2 dir = Vector2.zero;
            float wall = 0f;
            for (int i = 0; i < 16 && dir == Vector2.zero; i++)
            {
                var dd = Util.FromAngle(i * 22.5f);
                var hit = Physics2D.Raycast(b + Vector2.up * 0.25f, dd, 8.5f, Layers.ObstacleMask);
                if (hit.collider == null || hit.distance < 3.8f || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (Util.LineBlocked(b, b + dd * 3f)) continue;
                dir = dd;
                wall = hit.distance;
            }
            Assert.AreNotEqual(Vector2.zero, dir, "sandstone within a charge somewhere around the herd");
            hero.motor.Teleport(b + dir * Mathf.Min(3.2f, wall - 0.8f));
            bison.DebugCharge(hero);
            bool dazed = false;
            float until = Time.time + bison.chargeWindup + bison.chargeSeconds + 2.5f;
            while (Time.time < until && !dazed)
            {
                dazed = bison.Dazed;
                yield return null;
            }
            Assert.IsTrue(dazed, "it rams the rock and is dazed");
        }
    }
}
