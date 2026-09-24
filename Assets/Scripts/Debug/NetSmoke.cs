using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Automated check of online play with the built game, one server and two players on one
    /// machine (Tools/Server/netsmoke.ps1 starts them):
    ///   RungThiTham.exe -server -netsmoke -batchmode -nographics -port 7795 -data "…"
    ///   RungThiTham.exe -client 127.0.0.1 -port 7795 -login SmokeA matkhau -register -netsmoke [-batchmode -nographics]
    ///   RungThiTham.exe -client 127.0.0.1 -port 7795 -login SmokeB matkhau -register -netsmoke -batchmode -nographics
    /// A player waits for the other, walks and watches the other walk, then steps next to a Slime
    /// Rêu of its own (the two split them) and hits it until the server says it fell, and checks
    /// the server gave it the XP.
    /// With -netsmokeExpect "level xp" (a second run) it first checks the character came back as
    /// saved. The server quits once both players came and left. Exit code: 0 = all good, 1 = a
    /// check failed or errors were logged, 2 = something never happened in time. Does nothing in
    /// normal play.
    /// </summary>
    public class NetSmoke : MonoBehaviour
    {
        public static bool Active => Array.IndexOf(Environment.GetCommandLineArgs(), "-netsmoke") >= 0;

        /// <summary>How far the other hero must be seen walking for the run to pass.</summary>
        const float MinSeenWalk = 2f;

        int errors;
        string firstError;
        string role;

        void Start()
        {
            Application.logMessageReceived += CountErrors;
            role = GameSession.Mode.ToString().ToLowerInvariant();
            StartCoroutine(GameSession.Mode == SessionMode.Server ? RunServer() : RunPlayer());
        }

        void OnDestroy() => Application.logMessageReceived -= CountErrors;

        void CountErrors(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++;
            if (firstError == null) firstError = message;
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static string Arg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        void Finish(bool ok, string summary)
        {
            ok &= errors == 0;
            Debug.Log($"[NetSmoke] {role}: {summary}; {errors} errors{(firstError != null ? "; first: " + firstError : "")} → {(ok ? "ok" : "FAILED")}");
            Application.Quit(ok ? 0 : 1);
        }

        // ================================================================== server
        /// <summary>Stays until two players came and left <c>-netsmokeRounds</c> times (default once).</summary>
        IEnumerator RunServer()
        {
            int rounds = int.TryParse(Arg("-netsmokeRounds"), out int r) && r > 0 ? r : 1;
            float deadline = Time.realtimeSinceStartup + 200f * rounds;
            int most = 0, done = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                int now = ServerPlayers.I != null ? ServerPlayers.I.Count : 0;
                most = Mathf.Max(most, now);
                if (most >= 2 && now == 0)
                {
                    done++;
                    Debug.Log($"[NetSmoke] server: round {done} of {rounds} over");
                    if (done >= rounds) break;
                    most = 0;
                }
                yield return null;
            }
            if (done < rounds)
            {
                Debug.LogError($"[NetSmoke] server: players never came and left ({done} of {rounds} rounds)");
                Application.Quit(2);
                yield break;
            }
            yield return Wait(1f);
            Finish(true, $"{rounds} rounds of players came and left");
        }

        // ================================================================== player
        static PlayerController Other(PlayerController me)
        {
            foreach (var p in Players.All)
                if (p != null && p != me) return p;
            return null;
        }

        IEnumerator RunPlayer()
        {
            float deadline = Time.realtimeSinceStartup + 120f;
            while (OnlineSession.I == null || !OnlineSession.I.InWorld || Players.Local == null || Other(Players.Local) == null)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogError($"[NetSmoke] {role}: never got into the world with another player ({Players.All.Count} heroes)");
                    Application.Quit(2);
                    yield break;
                }
                yield return null;
            }
            var me = Players.Local;
            yield return Wait(1f);
            string expect = Arg("-netsmokeExpect");
            if (expect != null)
            {
                var parts = expect.Split(' ');
                int level = int.Parse(parts[0]), xp = int.Parse(parts[1]);
                bool back = me.stats.level == level && me.stats.xp == xp;
                Debug.Log($"[NetSmoke] {role}: came back at level {me.stats.level} xp {me.stats.xp} (expected {level} / {xp}) → {(back ? "restored" : "NOT restored")}");
                if (!back)
                {
                    Finish(false, "the character was not restored");
                    yield break;
                }
            }

            // ---------------------------------------------------------------- walking
            var other = Other(me);
            me.Autopilot = true;
            float seen = 0f;
            Vector2 last = other.transform.position;
            foreach (float dir in new[] { 1f, -1f, 1f, -1f })
            {
                me.SetIntent(new PlayerIntent { move = new Vector2(dir, 0f) });
                float end = Time.realtimeSinceStartup + 1.1f;
                while (Time.realtimeSinceStartup < end)
                {
                    if (other == null) break;
                    Vector2 now = other.transform.position;
                    seen += Vector2.Distance(now, last);
                    last = now;
                    yield return null;
                }
            }
            me.SetIntent(new PlayerIntent());
            Debug.Log($"[NetSmoke] {role}: saw the other hero walk {seen:0.0} units");

            // ---------------------------------------------------------------- a fight
            // each player takes a slime of its own (the two logins split them by id), steps next to
            // it (its own machine moves it, like a GM's tp) and hits it until the server says it fell
            int levelBefore = me.stats.level, xpBefore = me.stats.xp;
            int parity = string.IsNullOrEmpty(LoginInfo.Name) ? 0 : LoginInfo.Name[LoginInfo.Name.Length - 1] % 2;   // SmokeA / SmokeB
            EnemyBase slime = null;
            float fightUntil = Time.realtimeSinceStartup + 60f;
            bool killed = false, hitIt = false;
            while (Time.realtimeSinceStartup < fightUntil)
            {
                if (me.IsDead)
                {
                    yield return Wait(6f);   // fell: the server gets it up again
                    continue;
                }
                if (slime == null || !slime.gameObject.activeInHierarchy || slime.health.IsDead)
                {
                    if (slime != null && slime.health.IsDead && hitIt)
                    {
                        killed = true;
                        break;
                    }
                    slime = Pick(me.transform.position, parity);
                    hitIt = false;
                    if (slime == null)
                    {
                        yield return null;
                        continue;
                    }
                }
                Vector2 to = (Vector2)slime.transform.position - (Vector2)me.transform.position;
                if (to.magnitude > 1.4f)
                {
                    me.motor.Teleport((Vector2)slime.transform.position - to.normalized * 0.9f);
                    yield return Wait(0.2f);
                    continue;
                }
                me.SetIntent(new PlayerIntent { face = to.normalized, basicAttack = true, basicAttackAt = slime.transform.position });
                hitIt = true;
                yield return null;
            }
            me.SetIntent(new PlayerIntent());
            yield return Wait(1.5f);   // the server's character sheet arrives
            bool gotXp = me.stats.level > levelBefore || me.stats.xp > xpBefore;
            Debug.Log($"[NetSmoke] {role}: slime {(killed ? "fell" : "did not fall")}, level {levelBefore}→{me.stats.level}, xp {xpBefore}→{me.stats.xp}");
            Debug.Log($"[NetSmoke] {role}: character level {me.stats.level} xp {me.stats.xp} gold {me.inventory.gold}");
            yield return Shot();
            // the other player keeps fighting a little longer: stay so they still see this hero
            yield return Wait(GameSession.HasScreen ? 3f : 1f);
            Finish(seen >= MinSeenWalk && killed && gotXp, $"walk {seen:0.0}, kill {killed}, xp {gotXp}");
        }

        /// <summary>The nearest living Slime Rêu among this player's half of them (by id parity).</summary>
        static EnemyBase Pick(Vector2 from, int parity)
        {
            EnemyBase best = null;
            float bd = float.MaxValue;
            foreach (var e in EnemyBase.All)
            {
                if (e == null || e.enemyId != "slime" || e.health.IsDead || !e.gameObject.activeInHierarchy) continue;
                var ne = e.GetComponent<NetEntity>();
                if (ne != null && ne.Id % 2 != parity) continue;
                float d = ((Vector2)e.transform.position - from).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    best = e;
                }
            }
            return best;
        }

        IEnumerator Shot()
        {
            if (!GameSession.HasScreen || Application.isBatchMode) yield break;
            string dir = Arg("-netsmokeDir") ?? Path.Combine(Application.persistentDataPath, "netsmoke");
            Directory.CreateDirectory(dir);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(dir, $"netsmoke_{LoginInfo.Name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[NetSmoke] " + path);
            yield return null;
        }
    }
}
