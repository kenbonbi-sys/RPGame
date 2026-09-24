using System;
using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// A robot player for load tests (Tools/Server/loadtest.ps1, Docs/KeHoach-Online.md phase 5):
    ///   RungThiTham.exe -client 127.0.0.1 -port 7799 -login Bot7 matkhau -register -bot [-botSeconds 120] -batchmode -nographics
    /// It plays like a person would, through the same intents as the keyboard: walks (never
    /// teleports, so the server's move check sees honest moves), goes after the nearest enemy
    /// away from the boss, hits it, uses its skills and dashes now and then, drinks a potion when
    /// low, gets up after a fall, and roams near the village when nothing is around. After
    /// -botSeconds it writes what it did in its log and quits (0; 2 when it never got in; 0 too
    /// when the server went away while it played: the test stops the server under it on purpose).
    /// Does nothing in normal play.
    /// </summary>
    public class LoadBot : MonoBehaviour
    {
        public static bool Active => Array.IndexOf(Environment.GetCommandLineArgs(), "-bot") >= 0;

        /// <summary>Enemies further than this from the bot are left alone.</summary>
        const float Sight = 14f;
        /// <summary>Bots keep away from the bosses' arenas (a crowd of bots there would only fall).</summary>
        const float BossBerth = 20f;

        int casts, dashes, deaths, potions;
        // the game boots again (to the title, then offline) when the server goes away
        static bool wasInWorld;

        static string Arg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        IEnumerator Start()
        {
            float seconds = float.TryParse(Arg("-botSeconds"), out float s) && s > 0f ? s : 120f;
            if (wasInWorld && GameSession.Mode == SessionMode.Offline)
            {
                Debug.Log($"[Bot] {LoginInfo.Name}: the server went away ({LoginInfo.LastError})");
                Application.Quit(0);
                yield break;
            }
            float deadline = Time.realtimeSinceStartup + 90f;
            while (OnlineSession.I == null || !OnlineSession.I.InWorld || Players.Local == null)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogError($"[Bot] {LoginInfo.Name}: never got into the world");
                    Application.Quit(2);
                    yield break;
                }
                yield return null;
            }
            var me = Players.Local;
            wasInWorld = true;
            me.Autopilot = true;
            me.health.Died += _ => deaths++;
            int startLevel = me.stats.level, startXp = me.stats.xp;
            var rng = new System.Random(LoginInfo.Name.GetHashCode());
            float end = Time.realtimeSinceStartup + seconds;
            Vector2 roam = me.transform.position;
            float nextRoam = 0f, nextSkill = 2f, nextDash = 5f;
            Vector2 home = GameManager.I != null && GameManager.I.respawnPoint != null ? (Vector2)GameManager.I.respawnPoint.position : roam;
            while (Time.realtimeSinceStartup < end)
            {
                float now = Time.realtimeSinceStartup;
                if (me.IsDead)
                {
                    me.SetIntent(new PlayerIntent());
                    yield return new WaitForSecondsRealtime(0.5f);
                    continue;
                }
                var intent = new PlayerIntent();
                if (me.health.Fraction < 0.35f && rng.NextDouble() < 0.02)
                {
                    intent.potionPresses = 1;
                    potions++;
                }
                var target = Pick(me.transform.position);
                Vector2 at = me.transform.position;
                if (target != null)
                {
                    Vector2 to = (Vector2)target.transform.position - at;
                    intent.aim = target.transform.position;
                    if (to.magnitude > 1.3f) intent.move = to.normalized;
                    else
                    {
                        intent.face = to.normalized;
                        intent.basicAttack = true;
                        intent.basicAttackAt = target.transform.position;
                    }
                    if (now >= nextSkill)
                    {
                        nextSkill = now + 1.5f + (float)rng.NextDouble() * 2f;
                        intent.skillPresses |= 1 << (1 + rng.Next(3));   // the skills of slots 1–3
                        casts++;
                    }
                }
                else
                {
                    if (now >= nextRoam || Vector2.Distance(at, roam) < 0.6f)
                    {
                        nextRoam = now + 3f;
                        roam = home + new Vector2((float)rng.NextDouble() * 24f - 12f, (float)rng.NextDouble() * 16f - 8f);
                    }
                    intent.move = (roam - at).normalized;
                    intent.aim = roam;
                }
                if (now >= nextDash && intent.move.sqrMagnitude > 0.1f)
                {
                    nextDash = now + 4f + (float)rng.NextDouble() * 4f;
                    intent.skillPresses |= 1 << 7;   // Space: Lướt
                    dashes++;
                }
                me.SetIntent(intent);
                yield return new WaitForSecondsRealtime(0.1f);
            }
            me.SetIntent(new PlayerIntent());
            long ping = OnlineSession.I != null && OnlineSession.I.Network != null ? OnlineSession.I.Network.TimeManager.RoundTripTime : -1;
            Debug.Log($"[Bot] {LoginInfo.Name}: {seconds:0} s, casts {casts}, dashes {dashes}, potions {potions}, falls {deaths}, " +
                      $"level {startLevel}→{me.stats.level}, xp {startXp}→{me.stats.xp}, ping {ping} ms");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(0);
        }

        /// <summary>The nearest living enemy in sight, away from the bosses' arenas.</summary>
        static EnemyBase Pick(Vector2 from)
        {
            EnemyBase best = null;
            float bd = Sight * Sight;
            foreach (var e in EnemyBase.All)
            {
                if (e == null || e.IsDead || !e.gameObject.activeInHierarchy) continue;
                Vector2 p = e.transform.position;
                if (BossBase.All.Exists(b => b != null && Vector2.Distance(p, b.Home) < BossBerth)) continue;
                float d = (p - from).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    best = e;
                }
            }
            return best;
        }
    }
}
