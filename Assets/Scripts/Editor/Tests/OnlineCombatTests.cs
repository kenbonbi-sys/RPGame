using NUnit.Framework;
using UnityEngine;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Fighting in a shared world (Docs/KeHoach-Online.md §11): enemies' hits on a hero played from
    /// another machine wait half a ping on the server, so a dash pressed in time dodges them
    /// (<see cref="LagCompensation"/>); the boss grows with the heroes who fight it.
    /// Plain EditMode tests: no scene, no Play Mode.
    /// </summary>
    public class OnlineCombatTests
    {
        SessionMode mode;
        System.Func<float> savedRoll;
        System.Func<Health, float> savedHold;
        GameObject heroGo;
        Health hero;

        [SetUp]
        public void SetUp()
        {
            mode = GameSession.Mode;
            GameSession.Mode = SessionMode.Server;
            savedRoll = Health.SpreadRoll;
            Health.SpreadRoll = () => 0.5f;
            savedHold = LagCompensation.HoldOf;
            LagCompensation.HoldOf = h => 0.1f;   // a player with a 200 ms ping
            LagCompensation.Clear();
            heroGo = new GameObject("remote hero");
            hero = heroGo.AddComponent<Health>();
            hero.team = Team.Player;
            hero.ResetHealth(100f);
        }

        [TearDown]
        public void TearDown()
        {
            LagCompensation.Clear();
            LagCompensation.HoldOf = savedHold;
            Health.SpreadRoll = savedRoll;
            GameSession.Mode = mode;
            Object.DestroyImmediate(heroGo);
        }

        static DamageInfo EnemyHit(float amount)
        {
            var d = DamageInfo.Make(amount, Team.Enemy, null, Vector2.zero, Vector2.down);
            d.attackScaled = true;
            return d;
        }

        [Test]
        public void AnEnemysHitLandsHalfAPingLater()
        {
            Assert.AreEqual(0f, hero.TakeDamage(EnemyHit(10f)), "held on its way");
            Assert.AreEqual(100f, hero.hp);
            Assert.AreEqual(1, LagCompensation.PendingCount);
            LagCompensation.Tick(Time.time + 0.05f);
            Assert.AreEqual(100f, hero.hp, "not yet");
            LagCompensation.Tick(Time.time + 0.11f);
            Assert.AreEqual(90f, hero.hp, "then it lands");
            Assert.AreEqual(0, LagCompensation.PendingCount);
        }

        [Test]
        public void ADashPressedInTimeDodgesIt()
        {
            int evaded = 0;
            hero.Evaded += _ => evaded++;
            hero.TakeDamage(EnemyHit(30f));
            hero.invulnerable = true;   // the player's dash arrived meanwhile
            LagCompensation.Tick(Time.time + 0.2f);
            Assert.AreEqual(100f, hero.hp, "dodged, as on the player's screen");
            Assert.AreEqual(1, evaded, "and it counts as an evasion (Lướt Hoàn Hảo listens)");
        }

        [Test]
        public void OnlyEnemiesAttacksOnRemoteHeroesWait()
        {
            var dot = EnemyHit(5f);
            dot.dot = true;
            Assert.AreEqual(5f, hero.TakeDamage(dot), "damage over time lands at once");
            var script = EnemyHit(5f);
            script.pure = true;
            Assert.AreEqual(5f, hero.TakeDamage(script), "so does a scripted hit");

            LagCompensation.HoldOf = h => 0f;   // the host's own hero
            Assert.AreEqual(5f, hero.TakeDamage(EnemyHit(5f)), "a hero played on this machine is hit at once");

            LagCompensation.HoldOf = h => 0.1f;
            var enemyGo = new GameObject("enemy");
            var enemy = enemyGo.AddComponent<Health>();
            enemy.team = Team.Enemy;
            enemy.ResetHealth(100f);
            var mine = DamageInfo.Make(7f, Team.Player, null, Vector2.zero, Vector2.up);
            mine.attackScaled = true;
            Assert.AreEqual(7f, enemy.TakeDamage(mine), "heroes' own hits are not held");
            Object.DestroyImmediate(enemyGo);

            GameSession.Mode = SessionMode.Offline;
            Assert.AreEqual(5f, hero.TakeDamage(EnemyHit(5f)), "offline nothing waits");
            Assert.AreEqual(0, LagCompensation.PendingCount);
        }

        [Test]
        public void AHitNeverWaitsLongerThanTheCap()
        {
            LagCompensation.HoldOf = h => 2f;   // a machine claiming a huge ping
            hero.TakeDamage(EnemyHit(10f));
            LagCompensation.Tick(Time.time + LagCompensation.MaxHold + 0.001f);
            Assert.AreEqual(90f, hero.hp);
        }

        [Test]
        public void AHeroWhoLeftIsNotHit()
        {
            hero.TakeDamage(EnemyHit(10f));
            heroGo.SetActive(false);
            LagCompensation.Tick(Time.time + 1f);
            Assert.AreEqual(100f, hero.hp);
            Assert.AreEqual(0, LagCompensation.PendingCount);
        }

        /// <summary>Walks <paramref name="seconds"/> at <paramref name="speed"/> to the right, checked every 0.2 s; returns false at the first put-back.</summary>
        static bool Walk(MoveCheck check, ref Vector2 at, ref float now, float speed, float seconds)
        {
            for (float t = 0f; t < seconds - 0.001f; t += 0.2f)
            {
                at += Vector2.right * speed * 0.2f;
                now += 0.2f;
                if (!check.Check(at, 4.6f, now, out _)) return false;
            }
            return true;
        }

        [Test]
        public void WalkingAndDashingAreNeverPutBack()
        {
            var check = new MoveCheck();
            Vector2 at = Vector2.zero;
            float now = 0f;
            check.Reset(at);
            Assert.IsTrue(Walk(check, ref at, ref now, 4.6f, 5f), "walking at the hero's speed");
            Assert.IsTrue(Walk(check, ref at, ref now, 9f, 2f), "an update burst after a hiccup looks twice as fast: still fine");

            // a double dash whose request arrives a moment after its steps
            check.Reset(at);
            Assert.IsTrue(Walk(check, ref at, ref now, 4.6f, 1f));
            at += Vector2.right * 12f;
            now += 0.2f;
            Assert.IsTrue(check.Check(at, 4.6f, now, out _), "waits one more look before judging");
            check.Dashed(now + 0.05f);
            at += Vector2.right * 0.9f;
            now += 0.2f;
            Assert.IsTrue(check.Check(at, 4.6f, now, out _), "the dash counted");

            // a hard knockback pushes the hero further than it walks
            check.Reset(at);
            Assert.IsTrue(Walk(check, ref at, ref now, 4.6f, 1f));
            check.Pushed(20f, now);
            at += Vector2.right * 14f;
            now += 0.2f;
            Assert.IsTrue(check.Check(at, 4.6f, now, out _));
            now += 0.2f;
            Assert.IsTrue(check.Check(at, 4.6f, now, out _), "and it stays where the hit sent it");
        }

        [Test]
        public void ATeleportIsPutBack()
        {
            var check = new MoveCheck();
            Vector2 at = new Vector2(10f, 10f);
            float now = 0f;
            check.Reset(at);
            Assert.IsTrue(Walk(check, ref at, ref now, 4.6f, 1f));
            Vector2 before = at;
            at += new Vector2(40f, 0f);   // a cheat moves the hero 40 units at once
            now += 0.2f;
            Assert.IsTrue(check.Check(at, 4.6f, now, out _), "the first look waits");
            now += 0.2f;
            Assert.IsFalse(check.Check(at, 4.6f, now, out Vector2 back), "then it is put back");
            Assert.AreEqual(before, back, "where it stood before the jump");
            Assert.Greater(check.Moved, check.Allowed);

            // running three times too fast is caught too
            check.Reset(back);
            at = back;
            Assert.IsFalse(Walk(check, ref at, ref now, 4.6f * 3f, 3f));
        }

        [Test]
        public void TheBossGrowsWithEveryHeroInTheFight()
        {
            Assert.AreEqual(3200f, BossBear.MaxHpFor(3200f, 1, 0.7f), 0.01f, "one hero: as offline");
            Assert.AreEqual(3200f * 1.7f, BossBear.MaxHpFor(3200f, 2, 0.7f), 0.01f);
            Assert.AreEqual(3200f * 4.5f, BossBear.MaxHpFor(3200f, 6, 0.7f), 0.01f);
            Assert.AreEqual(3200f, BossBear.MaxHpFor(3200f, 0, 0.7f), 0.01f);

            var h = heroGo.GetComponent<Health>();
            h.ResetHealth(1000f);
            h.hp = 400f;
            h.ScaleMax(2000f);
            Assert.AreEqual(2000f, h.maxHp);
            Assert.AreEqual(800f, h.hp, 0.01f, "the same share of health left");
        }
    }
}
