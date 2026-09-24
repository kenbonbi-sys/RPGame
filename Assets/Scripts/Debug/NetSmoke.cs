using System;
using System.Collections;
using System.Collections.Generic;
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
    /// A player waits for the other, walks and watches the other walk, plays together (SmokeA
    /// invites SmokeB into a party, they talk on the party channel and whisper, SmokeA adds SmokeB
    /// as a friend and sees them playing), then steps next to a Slime Rêu of its own (the two split
    /// them) and hits it until the server says it fell, and checks the server gave it the XP.
    /// With -netsmokeAgain (a second run) SmokeA checks its friend is still listed, and with
    /// -netsmokeExpect "level xp" it first checks the character came back as saved. With -netsmokeSwitch n a lone player
    /// changes to channel n (/kenh n) and checks its character came along. The server quits once
    /// -netsmokePlayers players (2) came and left -netsmokeRounds times. Exit code: 0 = all good,
    /// 1 = a check failed or errors were logged, 2 = something never happened in time. Does
    /// nothing in normal play.
    /// </summary>
    public class NetSmoke : MonoBehaviour
    {
        public static bool Active => Array.IndexOf(Environment.GetCommandLineArgs(), "-netsmoke") >= 0;

        /// <summary>How far the other hero must be seen walking for the run to pass.</summary>
        const float MinSeenWalk = 2f;

        int errors;
        string firstError;
        string role;
        readonly List<string> heard = new List<string>();
        // a channel change boots the game again: what the player had before it
        static int switchedTo, levelBeforeSwitch = -1, xpBeforeSwitch, goldBeforeSwitch;

        void Start()
        {
            Application.logMessageReceived += CountErrors;
            GameEvents.Log += Heard;
            role = GameSession.Mode.ToString().ToLowerInvariant();
            if (GameSession.Mode == SessionMode.Server) StartCoroutine(RunServer());
            else if (int.TryParse(Arg("-netsmokeSwitch"), out int channel)) StartCoroutine(RunSwitch(channel));
            else StartCoroutine(RunPlayer());
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= CountErrors;
            GameEvents.Log -= Heard;
        }

        void Heard(string line, Color color) => heard.Add(line);

        /// <summary>Waits until a log line holding <paramref name="text"/> shows (true), or gives up.</summary>
        IEnumerator Hear(string text, float seconds, Action<bool> done)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                if (heard.Exists(l => l.Contains(text)))
                {
                    done(true);
                    yield break;
                }
                yield return null;
            }
            Debug.LogWarning($"[NetSmoke] {role}: never heard \"{text}\"");
            done(false);
        }

        static IEnumerator Until(Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

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
        /// <summary>Stays until <c>-netsmokePlayers</c> players (default two) came and left <c>-netsmokeRounds</c> times (default once).</summary>
        IEnumerator RunServer()
        {
            int rounds = int.TryParse(Arg("-netsmokeRounds"), out int r) && r > 0 ? r : 1;
            int players = int.TryParse(Arg("-netsmokePlayers"), out int n) && n > 0 ? n : 2;
            float deadline = Time.realtimeSinceStartup + 200f * rounds;
            int most = 0, done = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                int now = ServerPlayers.I != null ? ServerPlayers.I.Count : 0;
                most = Mathf.Max(most, now);
                if (most >= players && now == 0)
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

            // ---------------------------------------------------------------- a class, through the server
            // SmokeA becomes an elf ranger (the server checks it, saves it and shows it to everyone);
            // SmokeB must see A drawn so; coming back, A must still be one
            bool classOk = true;
            if (LoginInfo.Name != null && LoginInfo.Name.EndsWith("A"))
            {
                if (expect == null && !me.stats.HasClass)
                    CharacterChoice.Choose(new HeroLook { race = "elf", cls = "ranger", weapon = "bow", hair = 2, hairColor = 5 });
                yield return Until(() => me.stats.look.cls == "ranger", 8f);
                classOk = me.stats.look.cls == "ranger" && me.stats.look.race == "elf" && me.skills.slots[0] != null && me.skills.slots[0].id == "arrow";
                Debug.Log($"[NetSmoke] {role}: class {me.stats.look.race} {me.stats.look.cls}, Q {(me.skills.slots[0] != null ? me.skills.slots[0].id : "-")} → {(classOk ? "ok" : "NOT")}");
            }
            else
            {
                yield return Until(() => other != null && other.stats.look.cls == "ranger", 10f);
                // (drawn only where there is a screen: SmokeB may run without one)
                classOk = other != null && other.stats.look.cls == "ranger" && other.skills.slots[0] != null && other.skills.slots[0].id == "arrow";
                Debug.Log($"[NetSmoke] {role}: sees the other as {(other != null ? other.stats.look.race + " " + other.stats.look.cls : "-")} → {(classOk ? "ok" : "NOT")}");
            }

            // ---------------------------------------------------------------- playing together
            bool together = false;
            bool again = expect != null || Array.IndexOf(Environment.GetCommandLineArgs(), "-netsmokeAgain") >= 0;
            yield return Together(again, ok => together = ok);

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
            yield return Shot(again ? "_again" : "");
            // the other player keeps fighting a little longer: stay so they still see this hero
            yield return Wait(GameSession.HasScreen ? 3f : 1f);
            Finish(seen >= MinSeenWalk && killed && gotXp && together && classOk, $"walk {seen:0.0}, together {together}, kill {killed}, xp {gotXp}, class {classOk}");
        }

        /// <summary>
        /// SmokeA invites SmokeB; B says yes; A speaks on the party channel and B hears it; B
        /// whispers to A; A adds B as a friend and sees them playing. On the second run A only
        /// checks the friend is still on its list (the server kept it with the account).
        /// </summary>
        IEnumerator Together(bool secondRun, Action<bool> result)
        {
            string me = LoginInfo.Name ?? "";
            bool leads = me.EndsWith("A");
            string other = me.Length > 0 ? me.Substring(0, me.Length - 1) + (leads ? "B" : "A") : "";
            bool ok = true;
            if (secondRun)
            {
                if (leads)
                {
                    ChatCommands.Run("/banbe");
                    yield return Hear($"{other} (đang chơi)", 10f, h => ok = h);
                }
                Debug.Log($"[NetSmoke] {role}: friends kept {ok}");
                result(ok);
                yield break;
            }
            if (leads)
            {
                ChatCommands.Run("/moi " + other);
            }
            else
            {
                yield return Until(() => PartyState.InvitedBy != null, 15f);
                ok &= PartyState.InvitedBy == other;
                yield return Wait(0.3f);
                yield return Shot("_invite");
                PartyState.Answer(true);
            }
            yield return Until(() => PartyState.Names.Length == 2, 15f);
            bool party = PartyState.Names.Length == 2 && PartyState.IsLeader(leads ? me : other);
            ok &= party;
            yield return Wait(0.5f);
            yield return Shot("_party");
            bool partyChat = true, whisper = true, friend = true;
            if (leads)
            {
                ChatCommands.Run("/n chào tổ đội");
                yield return Hear($"{other} → bạn:", 15f, h => whisper = h);
                ChatCommands.Run("/ketban " + other);
                yield return Hear($"Đã thêm {other}", 10f, h => friend = h);
                ChatCommands.Run("/banbe");
                bool listed = false;
                yield return Hear($"{other} (đang chơi)", 10f, h => listed = h);
                friend &= listed;
            }
            else
            {
                yield return Hear($"[Tổ đội] {other}:", 15f, h => partyChat = h);
                ChatCommands.Run($"/w {other} bí mật");
                yield return Hear($"Bạn → {other}:", 10f, h => whisper = h);
            }
            ok &= partyChat && whisper && friend;
            Debug.Log($"[NetSmoke] {role}: party {party} ({PartyState.Names.Length} members, leader {PartyState.Leader}), party chat {partyChat}, whisper {whisper}, friend {friend}");
            result(ok);
        }

        // ================================================================== changing channel
        /// <summary>-netsmokeSwitch n: alone on channel 1, goes to channel n and checks the character came along.</summary>
        IEnumerator RunSwitch(int channel)
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            while (OnlineSession.I == null || !OnlineSession.I.InWorld || Players.Local == null)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogError($"[NetSmoke] {role}: never got into channel {(switchedTo > 0 ? switchedTo : 1)}");
                    Application.Quit(2);
                    yield break;
                }
                yield return null;
            }
            var me = Players.Local;
            yield return Wait(1f);
            if (switchedTo == 0)
            {
                ChatCommands.Run("/kenh");
                bool listed = false;
                yield return Hear($"Kênh {channel}: 0/", 10f, h => listed = h);
                levelBeforeSwitch = me.stats.level;
                xpBeforeSwitch = me.stats.xp;
                goldBeforeSwitch = me.inventory.gold;
                Debug.Log($"[NetSmoke] {role}: on channel {OnlineSession.Channel}, channels listed {listed}, level {levelBeforeSwitch} xp {xpBeforeSwitch} gold {goldBeforeSwitch}; going to channel {channel}");
                if (!listed)
                {
                    Finish(false, "the channels were not listed");
                    yield break;
                }
                switchedTo = channel;
                ChatCommands.Run("/kenh " + channel);
                yield return Wait(20f);   // the game boots again on the new channel: this component goes with it
                Finish(false, "the channel change never happened");
                yield break;
            }
            bool there = OnlineSession.Channel == switchedTo;
            bool same = me.stats.level == levelBeforeSwitch && me.stats.xp == xpBeforeSwitch && me.inventory.gold == goldBeforeSwitch;
            Debug.Log($"[NetSmoke] {role}: now on channel {OnlineSession.Channel}, level {me.stats.level} xp {me.stats.xp} gold {me.inventory.gold}");
            yield return Wait(1f);
            Finish(there && same, $"changed to channel {OnlineSession.Channel} {(there ? "ok" : "WRONG")}, character {(same ? "came along" : "DIFFERENT")}");
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

        IEnumerator Shot(string suffix)
        {
            if (!GameSession.HasScreen || Application.isBatchMode) yield break;
            string dir = Arg("-netsmokeDir") ?? Path.Combine(Application.persistentDataPath, "netsmoke");
            Directory.CreateDirectory(dir);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(dir, $"netsmoke_{LoginInfo.Name}{suffix}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[NetSmoke] " + path);
            yield return null;
        }
    }
}
