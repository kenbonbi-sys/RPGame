using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// The deeper Hang Pha Lê in the real world scene (plays it offline, like <see cref="CaveTests"/>):
    /// Bọ Giáp Đá's shell and its charge into rock, Slime Pha Lê turning shots back, Mắt Hang's
    /// bouncing beam, Mimic Tham Lam's greed, the old golem's core, and the spider queen's beam
    /// sent back into her by a struck pillar.
    /// </summary>
    public class CaveDeepTests
    {
        string tempSaves;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempSaves = Path.Combine(Path.GetTempPath(), "rtt_test_saves_cavedeep");
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

        static int Count(string id) => EnemyBase.All.FindAll(e => e.enemyId == id).Count;

        /// <summary>A blow of <paramref name="amount"/> coming at <paramref name="h"/> from <paramref name="from"/> (nobody's, so only its direction tells).</summary>
        static float HitFrom(Health h, Vector2 from, float amount = 100f)
        {
            Vector2 at = h.transform.position;
            var d = DamageInfo.Make(amount, Team.Player, null, at, (at - from).normalized, DamageType.Physical, 0f);
            return h.TakeDamage(d);
        }

        [UnityTest]
        public IEnumerator TheDeepCaveHasItsCreatures()
        {
            var zone = ZoneRoot.Current;
            Assert.GreaterOrEqual(Count("beetle"), 4, "stone beetles in the mine and the loop");
            Assert.GreaterOrEqual(Count("crystalslime"), 5, "crystal slimes among the crystals");
            Assert.AreEqual(4, Count("caveeye"), "an eye in four chambers' walls");
            Assert.AreEqual(1, Count("mimic"), "one mimic");
            foreach (var e in EnemyBase.All)
                if (e.enemyId == "caveeye") Assert.IsFalse(zone.IsWall(e.transform.position), "an eye sits on the cave floor");
            var mimic = EnemyBase.All.Find(e => e.enemyId == "mimic") as MimicAI;
            Assert.NotNull(mimic);
            Assert.GreaterOrEqual(mimic.hideSpots.Length, 4);
            foreach (var s in mimic.hideSpots) Assert.IsFalse(zone.IsWall(s), "the mimic hides on the floor");
            Assert.IsTrue(mimic.Disguised, "it waits as a chest");
            var golem = BossBase.Find("crystalgolem");
            Assert.NotNull(golem, "Golem Pha Lê Cổ");
            Assert.AreEqual(EnemyRank.MiniBoss, golem.rank);
            Assert.Less(Vector2.Distance(golem.Home, Spot("crystalhall")), 2f, "in the crystal hall");
            var queen = BossBase.Find("spiderqueen");
            Assert.NotNull(queen, "Nhện Chúa Pha Lê");
            Assert.AreEqual(EnemyRank.Boss, queen.rank);
            Assert.Less(Vector2.Distance(queen.Home, Spot("queenhall")), 2f, "in her hall");
            Assert.AreEqual(6, CrystalPillar.All.FindAll(p => Vector2.Distance(p.transform.position, queen.Home) < 10f).Count, "six pillars ring her hall");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ABeetlesShellTurnsBlowsAside()
        {
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var beetle = Nearest("beetle", Spot("mine")) as StoneBeetleAI;
            Assert.NotNull(beetle);
            Vector2 b = beetle.transform.position;
            float face = beetle.Facing;
            beetle.health.ResetHealth();
            float front = HitFrom(beetle.health, b + new Vector2(face * 2f, 0f));
            beetle.health.ResetHealth();
            float back = HitFrom(beetle.health, b + new Vector2(-face * 2f, 0f));
            beetle.health.ResetHealth();
            Assert.Greater(back, front * 5f, $"its back ({back}) takes far more than its shell ({front})");

            // its charge into rock leaves it dazed: find rock a few steps away, a hero between
            Vector2 dir = Vector2.zero;
            float wall = 0f;
            for (int i = 0; i < 16 && dir == Vector2.zero; i++)
            {
                var d = Util.FromAngle(i * 22.5f);
                var hit = Physics2D.Raycast(b + Vector2.up * 0.25f, d, 6.5f, Layers.ObstacleMask);
                if (hit.collider == null || hit.distance < 3.8f || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (Util.LineBlocked(b, b + d * 3f)) continue;
                dir = d;
                wall = hit.distance;
            }
            Assert.AreNotEqual(Vector2.zero, dir, "rock within a charge somewhere around it");
            hero.motor.Teleport(b + dir * Mathf.Min(3f, wall - 0.8f));
            beetle.DebugCharge(hero);
            bool dazed = false;
            float until = Time.time + beetle.chargeWindup + beetle.chargeSeconds + 2.5f;   // it may first have to wake up
            while (Time.time < until && !dazed)
            {
                dazed = beetle.Dazed;
                yield return null;
            }
            Assert.IsTrue(dazed, "it rams the rock and is dazed");
            Assert.IsTrue(beetle.status.IsStunned);
        }

        [UnityTest]
        public IEnumerator CrystalSlimeTurnsShotsBack()
        {
            var hero = Players.Local;
            hero.health.ResetHealth(5000f);
            var slime = Nearest("crystalslime", Spot("crystalforest"));
            Assert.NotNull(slime);
            Assert.NotNull(slime.GetComponent<ShotReflector>());
            Vector2 s = slime.transform.position;
            Vector2 from = s + Vector2.left * 3f;
            foreach (var d in new[] { Vector2.left, Vector2.right, Vector2.down, Vector2.up })
                if (!Util.LineBlocked(s, s + d * 3f) && !ZoneRoot.Current.IsWall(s + d * 3f)) { from = s + d * 3f; break; }
            hero.motor.Teleport(from);
            // hold it still so the shot cannot miss (its crystal skin works whatever it is doing)
            slime.enabled = false;
            slime.motor.HardStop();
            yield return null;
            float heroBefore = hero.health.hp;
            var db = GameManager.I.db;
            var go = Pool.Get(db.fireballPrefab, from + (s - from).normalized * 0.8f, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            var shot = go.GetComponent<Projectile>();
            shot.team = Team.Player;
            shot.damage = 60f;
            shot.speed = 12f;
            shot.lifetime = 1f;
            shot.explodeRadius = 0f;
            shot.pierce = false;
            shot.Launch((Vector2)slime.transform.position - (Vector2)go.transform.position, hero.gameObject);
            yield return GameSmokeTests.GameSeconds(1.2f);
            Assert.AreEqual(slime.health.maxHp, slime.health.hp, 0.01f, "the shot did not hurt it");
            Assert.Less(hero.health.hp, heroBefore, "a splinter came back at the one who fired");
            // a blade still bites
            var cut = DamageInfo.Make(40f, Team.Player, hero.gameObject, slime.transform.position, Vector2.right, DamageType.Physical, 0f);
            Assert.Greater(slime.health.TakeDamage(cut), 0f);
        }

        [UnityTest]
        public IEnumerator AnEyesBeamBouncesOffRock()
        {
            // a beam aimed into rock at a slant comes off it
            Vector2 c = Spot("crystalforest");
            var path = EnemyShots.Trace(c, new Vector2(1f, 1f), 30f);
            Assert.IsTrue(path.bounced, "it met rock");
            Assert.Greater(Vector2.Distance(path.b, path.c), 1f, "and went on after it");
            // an eye stares at a hero in sight and fires
            var hero = Players.Local;
            hero.health.ResetHealth(5000f);
            CaveEyeAI eye = null;
            Vector2 stand = default;
            foreach (var e in EnemyBase.All)
            {
                if (!(e is CaveEyeAI ce)) continue;
                Vector2 p = ce.transform.position;
                foreach (var d in new[] { Vector2.down, new Vector2(0.6f, -0.8f), new Vector2(-0.6f, -0.8f) })
                {
                    var q = p + d * 4f;
                    if (ZoneRoot.Current.IsWall(q) || Util.LineBlocked(p + Vector2.up * 0.45f, q + Vector2.up * 0.35f)) continue;
                    eye = ce;
                    stand = q;
                    break;
                }
                if (eye != null) break;
            }
            Assert.NotNull(eye, "an eye with open floor below it");
            Assert.IsFalse(eye.Open, "shut while nobody is near");
            hero.motor.Teleport(stand);
            yield return GameSmokeTests.GameSeconds(1f);   // it wakes (nobody was near)
            hero.motor.Teleport(stand);
            float before = hero.health.hp;
            eye.DebugBeam(hero);
            Assert.IsTrue(eye.Open, "it opens to stare");
            yield return GameSmokeTests.GameSeconds(eye.stare + 0.4f);
            Assert.Less(hero.health.hp, before, "the beam hit");
            // shut, its lid of stone takes most of a blow; open, it takes more
            float open = HitFrom(eye.health, stand);
            yield return GameSmokeTests.GameSeconds(eye.openAfter + 0.5f);
            Assert.IsFalse(eye.Open);
            float shut = HitFrom(eye.health, stand);
            Assert.Greater(open, shut * 3f, $"open ({open}) against shut ({shut})");
        }

        [UnityTest]
        public IEnumerator TheMimicStealsAndPaysBackDouble()
        {
            var hero = Players.Local;
            hero.health.ResetHealth(5000f);
            hero.inventory.gold = 1000;
            var mimic = EnemyBase.All.Find(e => e.enemyId == "mimic") as MimicAI;
            Assert.NotNull(mimic);
            Vector2 m = mimic.transform.position;
            Vector2 stand = m + Vector2.left * 1.1f;
            foreach (var d in new[] { Vector2.left, Vector2.right, Vector2.down, Vector2.up })
                if (!ZoneRoot.Current.IsWall(m + d * 1.1f) && !Util.LineBlocked(m, m + d * 1.1f)) { stand = m + d * 1.1f; break; }
            hero.motor.Teleport(stand);
            float until = Time.time + 6f;
            while (Time.time < until && mimic.StolenFrom(hero) == 0)
            {
                hero.motor.Teleport(stand);   // stay right next to it
                yield return null;
            }
            Assert.IsFalse(mimic.Disguised, "the chest sprang");
            int stolen = mimic.StolenFrom(hero);
            Assert.Greater(stolen, 0, "its bite swallowed gold");
            Assert.AreEqual(1000 - stolen, hero.inventory.gold);
            mimic.health.Kill();
            yield return null;
            Assert.AreEqual(1000 + stolen, hero.inventory.gold, "felled, it gives back twice what it took");
        }

        [UnityTest]
        public IEnumerator TheMimicIsGoneTheThirdTimeItDigsDown()
        {
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var mimic = EnemyBase.All.Find(e => e.enemyId == "mimic") as MimicAI;
            Assert.NotNull(mimic);
            hero.motor.Teleport((Vector2)mimic.transform.position + Vector2.down * 4f);
            mimic.DebugSpring();
            yield return GameSmokeTests.GameSeconds(1f);
            for (int i = 1; i <= mimic.escapeAt; i++)
            {
                mimic.DebugBurrow();
                Assert.AreEqual(i, mimic.Burrows);
                // follow it wherever it comes up, so it does not give up and dig down by itself
                float until = Time.time + 2.2f;
                while (Time.time < until && mimic.gameObject.activeInHierarchy)
                {
                    hero.motor.Teleport((Vector2)mimic.transform.position + Vector2.down * 3f);
                    yield return null;
                }
                if (i < mimic.escapeAt) Assert.IsTrue(mimic.gameObject.activeInHierarchy, $"it came up again ({i})");
            }
            Assert.IsFalse(mimic.gameObject.activeInHierarchy, "the third time it is gone");
        }

        [UnityTest]
        public IEnumerator TheOldGolemsCoreIsInItsBack()
        {
            var golem = BossBase.Find("crystalgolem") as BossCrystalGolem;
            Assert.NotNull(golem);
            Vector2 g = golem.transform.position;
            float face = golem.Facing;
            var mirror = golem.GetComponent<ShotReflector>();
            Assert.NotNull(mirror);
            Assert.IsTrue(mirror.frontOnly);
            Assert.IsTrue(mirror.Faces(g + new Vector2(face * 3f, 0f)), "shots at its face come back");
            Assert.IsFalse(mirror.Faces(g + new Vector2(-face * 3f, 0f)), "shots at its back do not");
            float front = HitFrom(golem.health, g + new Vector2(face * 2f, 0f));
            float back = HitFrom(golem.health, g + new Vector2(-face * 2f, 0f));
            Assert.Greater(back, front * 2.5f, $"its core ({back}) against its crystal face ({front})");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AStruckPillarSendsTheQueensBeamBack()
        {
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var queen = BossBase.Find("spiderqueen") as BossSpiderQueen;
            Assert.NotNull(queen);
            Vector2 q = queen.transform.position;
            CrystalPillar mirror = null;
            foreach (var pillar in CrystalPillar.All)
            {
                if (pillar == null || Vector2.Distance(pillar.transform.position, q) > 10f) continue;
                Vector2 foot = (Vector2)pillar.transform.position + Vector2.up * 0.3f;
                Vector2 eye = q + new Vector2(Mathf.Sign(foot.x - q.x) * 0.9f, 0.9f);
                Vector2 stand = foot + (foot - eye).normalized * 2.2f - Vector2.up * 0.35f;
                if (ZoneRoot.Current.IsWall(stand)) continue;
                hero.motor.Teleport(stand);
                yield return null;
                Assert.IsTrue(queen.DebugForce("beam"));
                yield return null;
                if (queen.Mirror == null) continue;
                mirror = queen.Mirror;
                break;
            }
            Assert.NotNull(mirror, "her beam is aimed off a pillar");
            // strike the pillar while she stares
            mirror.regrowSeconds = 3f;
            var hit = DamageInfo.Make(10f, Team.Player, hero.gameObject, mirror.transform.position, Vector2.right, DamageType.Physical, 0f);
            mirror.Health.TakeDamage(hit);
            yield return GameSmokeTests.GameSeconds(0.3f);
            Assert.AreEqual(1, queen.Backfires, "the beam went back into her");
            Assert.IsTrue(mirror.Broken, "the pillar shattered");
            Assert.IsTrue(queen.status.IsStunned, "and she reels");
            // it grows back
            yield return GameSmokeTests.GameSeconds(mirror.regrowSeconds + 1.5f);
            Assert.IsFalse(mirror.Broken, "the pillar grew back");
        }
    }
}
