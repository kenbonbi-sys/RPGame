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
    /// SmokeA, a ranger, is given a Bí Kíp by the server, reads it and puts Băng Tiễn on W (the server
    /// checks both); SmokeB must see A's W become Băng Tiễn, and coming back A still has it there.
    /// On the first run SmokeA also flies over Khe Vực on a Cột Gió while SmokeB watches from the
    /// far rim: B must see A rise above the ravine and come down on its side.
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
                GiveBooks();
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

        readonly HashSet<PlayerController> booked = new HashSet<PlayerController>();

        /// <summary>A ranger that does not know Băng Tiễn gets its Bí Kíp, once (Sách Chiêu over the network).</summary>
        void GiveBooks()
        {
            var tome = GameManager.I != null ? GameManager.I.db.Item("tome_frostarrows") : null;
            if (tome == null) return;
            foreach (var p in Players.All)
                if (p != null && p.stats != null && p.inventory != null && p.stats.look.cls == "ranger" &&
                    !Spellbook.Knows(p.stats.look, "frostarrows") && p.inventory.Count(tome) == 0 && booked.Add(p))
                    p.inventory.Add(tome, 1, false);
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

            // ---------------------------------------------------------------- gear, through the server
            // SmokeA puts the starting kit's Kiếm Sắt in its off hand (the server does it, sends the
            // bag back and saves it); coming back, it must still be worn
            bool gearOk = true;
            if (LoginInfo.Name != null && LoginInfo.Name.EndsWith("A"))
            {
                var sword = GameManager.I.db.Item("sword");
                if (expect == null && me.inventory.Worn(EquipSlot.Offhand) != sword) me.inventory.AskEquip(sword);
                yield return Until(() => me.inventory.Worn(EquipSlot.Offhand) == sword, 8f);
                gearOk = me.inventory.Worn(EquipSlot.Offhand) == sword && me.inventory.Count(sword) == 0;
                Debug.Log($"[NetSmoke] {role}: off hand {(me.inventory.Worn(EquipSlot.Offhand) != null ? me.inventory.Worn(EquipSlot.Offhand).id : "-")} → {(gearOk ? "ok" : "NOT")}");
            }

            // ---------------------------------------------------------------- Sách Chiêu, through the server
            // SmokeA reads the Bí Kíp the server gave it and puts Băng Tiễn on W (the server checks
            // both, saves them and shows them to everyone); SmokeB must see A's W change
            bool spellOk = true;
            if (LoginInfo.Name != null && LoginInfo.Name.EndsWith("A"))
            {
                if (!Spellbook.Knows(me.stats.look, "frostarrows"))
                {
                    var tome = GameManager.I.db.Item("tome_frostarrows");
                    yield return Until(() => me.inventory.Count(tome) > 0, 10f);
                    me.UseItem(tome);
                    yield return Until(() => Spellbook.Knows(me.stats.look, "frostarrows"), 8f);
                }
                if (Spellbook.OnBar(me.stats.look, 1) != "frostarrows") Spellbook.Ask(1, "frostarrows");
                yield return Until(() => me.skills.slots[1] != null && me.skills.slots[1].id == "frostarrows", 8f);
                spellOk = Spellbook.Knows(me.stats.look, "frostarrows") && me.skills.slots[1] != null && me.skills.slots[1].id == "frostarrows" &&
                          me.inventory.Count(GameManager.I.db.Item("tome_frostarrows")) == 0;
                Debug.Log($"[NetSmoke] {role}: W {(me.skills.slots[1] != null ? me.skills.slots[1].id : "-")}, knows {string.Join(",", me.stats.look.spells ?? new string[0])} → {(spellOk ? "ok" : "NOT")}");
            }
            else
            {
                yield return Until(() => other != null && other.skills.slots[1] != null && other.skills.slots[1].id == "frostarrows", 25f);
                spellOk = other != null && other.skills.slots[1] != null && other.skills.slots[1].id == "frostarrows";
                Debug.Log($"[NetSmoke] {role}: sees the other's W as {(other != null && other.skills.slots[1] != null ? other.skills.slots[1].id : "-")} → {(spellOk ? "ok" : "NOT")}");
            }

            // ---------------------------------------------------------------- playing together
            bool together = false;
            bool again = expect != null || Array.IndexOf(Environment.GetCommandLineArgs(), "-netsmokeAgain") >= 0;
            yield return Together(again, ok => together = ok);

            // ---------------------------------------------------------------- a flight over Khe Vực
            bool flightOk = true;
            if (!again) yield return Flight(ok => flightOk = ok);

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
            Finish(seen >= MinSeenWalk && killed && gotXp && together && classOk && gearOk && spellOk && flightOk,
                   $"walk {seen:0.0}, together {together}, kill {killed}, xp {gotXp}, class {classOk}, gear {gearOk}, spell {spellOk}, flight {flightOk}");
        }

        /// <summary>
        /// Thảo Nguyên Gió: SmokeA steps into the east Cột Gió of the middle crossing and its own
        /// machine flies it over Khe Vực; SmokeB waits on the far rim and must see A's body rise
        /// above the ravine (its screen draws that itself) and come down on B's side. Both hold
        /// their spot against the steppe's wind while they wait.
        /// </summary>
        IEnumerator Flight(Action<bool> result)
        {
            var me = Players.Local;
            var zone = ZoneRoot.Current;
            var east = WindColumn.All.Find(c => c != null && c.columnId == "e96");
            if (east == null || east.partner == null || zone == null)
            {
                Debug.LogError($"[NetSmoke] {role}: no Cột Gió e96 in the world");
                result(false);
                yield break;
            }
            var west = east.partner;
            Vector2 e = east.transform.position, w = west.transform.position;
            bool leads = (LoginInfo.Name ?? "").EndsWith("A");
            if (leads)
            {
                Vector2 spot = e + Vector2.right * 3f;
                me.motor.Teleport(spot);
                yield return Hold(me, spot, 3f);   // B gets to the far rim and sees A arrive
                float until = Time.realtimeSinceStartup + 6f;
                while (!WindColumn.Carrying && Time.realtimeSinceStartup < until)
                {
                    Vector2 to = e - (Vector2)me.transform.position;
                    me.SetIntent(new PlayerIntent { move = to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.left });
                    yield return null;
                }
                me.SetIntent(new PlayerIntent());
                bool flew = WindColumn.Carrying;
                yield return Until(() => !WindColumn.Carrying, 6f);
                Vector2 landed = me.transform.position;
                yield return Hold(me, landed, 2.5f);   // B watches the landing
                bool across = me.transform.position.x < w.x + 1f && !zone.IsChasm(me.transform.position);
                Debug.Log($"[NetSmoke] {role}: flew over Khe Vực {flew}, now at ({me.transform.position.x:0.0}, {me.transform.position.y:0.0}) → {(flew && across ? "across" : "NOT across")}");
                result(flew && across);
                yield break;
            }
            var other = Other(me);
            // on the far rim, out of the Hắc Phong archers' sight
            Vector2 mine = west.Landing + new Vector2(-1f, 2f);
            me.motor.Teleport(mine);
            float wait = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < wait && (other == null || Vector2.Distance(other.transform.position, e) > 4f))
                yield return Hold(me, mine, 0f);
            var body = other != null && other.anim != null ? other.anim.transform : null;
            float rest = body != null ? body.localPosition.y : 0f, top = 0f;
            float watch = Time.realtimeSinceStartup + 9f;
            while (Time.realtimeSinceStartup < watch && other != null && other.transform.position.x > w.x + 1f)
            {
                if (body != null) top = Mathf.Max(top, body.localPosition.y - rest);
                yield return Hold(me, mine, 0f);
            }
            yield return Hold(me, mine, 0.8f);
            bool arrived = other != null && other.transform.position.x < w.x + 1f;
            bool down = body != null && Mathf.Abs(body.localPosition.y - rest) < 0.05f;
            bool ok = arrived && down && top > WindColumn.CarryHeight * 0.6f;
            Debug.Log($"[NetSmoke] {role}: saw the other fly over Khe Vực, {top:0.00} above its shadow at most, {(arrived ? "landed" : "NOT landed")} on this rim, {(down ? "down" : "still raised")} → {(ok ? "ok" : "NOT")}");
            // the Hắc Phong camp across the road: the server sends its bandits, this screen draws them in their looks
            EnemyBase bandit = null;
            yield return Until(() => (bandit = EnemyBase.All.Find(e => e != null && e.enemyId == "hp_archer" && e.gameObject.activeInHierarchy)) != null, 5f);
            bool dressed = bandit != null && bandit.anim != null && bandit.anim.set != null && bandit.anim.set.name.EndsWith("_dressed");
            Debug.Log($"[NetSmoke] {role}: a Hắc Phong archer {(bandit != null ? "in sight" : "NOT in sight")}, {(dressed ? "drawn in its look" : "NOT dressed")}");
            result(ok && dressed);
        }

        /// <summary>Keeps <paramref name="me"/> at <paramref name="spot"/> against the wind for <paramref name="seconds"/> (0: one frame).</summary>
        static IEnumerator Hold(PlayerController me, Vector2 spot, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            do
            {
                Vector2 to = spot - (Vector2)me.transform.position;
                me.SetIntent(new PlayerIntent { move = to.magnitude > 0.3f ? to.normalized : Vector2.zero });
                yield return null;
            } while (Time.realtimeSinceStartup < end);
            me.SetIntent(new PlayerIntent());
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
