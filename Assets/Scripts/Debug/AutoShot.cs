using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Automated showcase used for testing builds: start the player with
    ///   Game.exe -autoshot -autoshotDir "C:\shots" [-autoshotTimeout 480] [-autoshotOnly swamp|cave]
    /// It plays a scripted tour (dialogue, skills, boss attacks, the swamp and its bosses, the
    /// world map, night), saves screenshots and quits; -autoshotOnly swamp tours the swamp alone,
    /// -autoshotOnly cave the crystal cave.
    /// Exit code: 0 = clean run, 1 = errors or exceptions were logged, 2 = the tour did not finish
    /// within the timeout (CI reads it). Does nothing in normal play.
    /// </summary>
    public class AutoShot : MonoBehaviour
    {
        public static bool Active => Array.IndexOf(Environment.GetCommandLineArgs(), "-autoshot") >= 0;

        string dir;
        int index;
        int errors;
        string firstError;

        void Start()
        {
            if (!Active)
            {
                enabled = false;
                return;
            }
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-autoshotDir");
            dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.persistentDataPath, "autoshot");
            Directory.CreateDirectory(dir);
            i = Array.IndexOf(args, "-autoshotTimeout");
            float timeout = i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float t) ? t : 480f;
            Application.logMessageReceived += CountErrors;
            StartCoroutine(Run());
            StartCoroutine(Watchdog(timeout));
        }

        void OnDestroy() => Application.logMessageReceived -= CountErrors;

        void CountErrors(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++;
            if (firstError == null) firstError = message;
        }

        /// <summary>A tour step that throws stops the tour; never leave the player running.</summary>
        IEnumerator Watchdog(float seconds)
        {
            yield return Wait(seconds);
            Debug.LogError($"[AutoShot] tour did not finish within {seconds:0} s ({index} shots)");
            Application.Quit(2);
        }

        void Finish()
        {
            if (errors > 0) Debug.Log($"[AutoShot] done: {index} shots, {errors} errors; first: {firstError}");
            else Debug.Log($"[AutoShot] done: {index} shots, no errors");
            Application.Quit(errors > 0 ? 1 : 0);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(dir, $"{index++:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[AutoShot] " + path);
            yield return null;
        }

        static IEnumerator Wait(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>Clicks through the current conversation, taking the first choice.</summary>
        static IEnumerator FinishDialogue()
        {
            for (int guard = 0; guard < 400 && DialogueDirector.I != null && DialogueDirector.I.IsRunning; guard++)
            {
                if (DialogueUI.I.ShowingOptions) DialogueUI.I.Choose(0);
                else DialogueUI.I.DebugAdvance();
                yield return Wait(0.05f);
            }
        }

        void Prep(PlayerController p)
        {
            p.energy = p.maxEnergy;
            p.skills.ResetCooldowns();
        }

        void Place(PlayerController p, Vector2 at)
        {
            p.motor.Teleport(at);
            if (CameraRig.I != null) CameraRig.I.SnapToTarget();
        }

        EnemyBase FindEnemy(string id, Vector2 near)
        {
            EnemyBase best = null;
            float bd = float.MaxValue;
            foreach (var e in EnemyBase.All)
            {
                if (e.IsDead || e.enemyId != id) continue;
                float d = Vector2.Distance(e.transform.position, near);
                if (d < bd)
                {
                    bd = d;
                    best = e;
                }
            }
            return best;
        }

        /// <summary>The part of the tour asked for with -autoshotOnly (null: all of it).</summary>
        static string Only
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-autoshotOnly");
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }
        }

        /// <summary>
        /// Đầm Lầy Sương Mù: the road in, the outpost and its Đá Truyền Tống, the world map, wading,
        /// leeches, toads, a mud man's split, a water snake's strike, a dragonfly's dart, Cóc Tía's
        /// and Xà Mẫu's attacks, the wisps that come out at night, and the trip home through the stones.
        /// </summary>
        IEnumerator Swamp(PlayerController p, DayNightCycle dn)
        {
            var zone = ZoneRoot.Current;
            Transform Spot(string id) => zone != null ? zone.SpotOf(id) : null;
            if (dn != null) dn.time = 0.42f;

            Place(p, new Vector2(99f, 29.2f));
            yield return Wait(1.5f);
            yield return Shot("swamp_edge");
            var outpost = Waystone.Find("outpost");
            if (outpost != null)
            {
                Place(p, outpost.Arrival + new Vector2(0.6f, 0f));
                yield return Wait(1.4f);
                yield return Shot("outpost_waystone");
                // wake every stone for the map, the outpost's last (where the hero gets up)
                foreach (var w in Waystone.All)
                    if (w != outpost && p.waystones != null) p.waystones.Touch(w);
                if (p.waystones != null) p.waystones.Touch(outpost);
                yield return Wait(0.3f);
                if (WorldMapUI.I != null)
                {
                    WorldMapUI.I.Show();
                    yield return Wait(0.8f);
                    yield return Shot("world_map");
                    WorldMapUI.I.Close();
                    yield return Wait(0.3f);
                }
            }

            // wading into the leeches' pool
            var leech = FindEnemy("leech", new Vector2(124f, 33f));
            if (leech != null)
            {
                Place(p, (Vector2)leech.transform.position + new Vector2(-2.2f, 0.2f));
                yield return Wait(2.6f);
                yield return Shot("leech_pool");
            }

            // toads spitting
            var swamp = Spot("swamp");
            var toad = FindEnemy("toad", swamp != null ? (Vector2)swamp.position : new Vector2(121f, 20.5f));
            if (toad != null)
            {
                Place(p, (Vector2)toad.transform.position + new Vector2(-4.2f, -0.8f));
                yield return Wait(2.4f);
                yield return Shot("toads");
            }

            // a mud man slams, falls and splits
            var field = Spot("mudfield");
            var mud = FindEnemy("mudman", field != null ? (Vector2)field.position : new Vector2(141f, 18f));
            if (mud != null)
            {
                Place(p, (Vector2)mud.transform.position + new Vector2(-1.8f, -0.4f));
                yield return Wait(2.2f);
                yield return Shot("mudman");
                mud.health.Kill();
                yield return Wait(0.9f);
                yield return Shot("mudman_split");
                yield return Wait(0.5f);
            }

            // a water snake rising from its pool to strike
            var pools = Spot("snakepools");
            var ws = FindEnemy("watersnake", pools != null ? (Vector2)pools.position : new Vector2(133f, 35.5f)) as WaterSnakeAI;
            if (ws != null)
            {
                Place(p, (Vector2)ws.transform.position + new Vector2(-3.4f, -1f));
                yield return Wait(1.2f);
                yield return Shot("watersnake_hidden");
                ws.DebugStrike(p);
                yield return Wait(0.35f);
                yield return Shot("watersnake_windup");
                yield return Wait(0.4f);
                yield return Shot("watersnake_lunge");
                yield return Wait(1.6f);
            }

            // dragonflies over the mire by day: one darts through the hero
            var flies = Spot("dragonflies");
            var fly = FindEnemy("dragonfly", flies != null ? (Vector2)flies.position : new Vector2(139f, 29f)) as DragonflyAI;
            if (fly != null)
            {
                Place(p, (Vector2)fly.transform.position + new Vector2(-2.8f, -1.2f));
                yield return Wait(1.4f);
                yield return Shot("dragonflies");
                fly.DebugDart(p);
                yield return Wait(0.3f);
                yield return Shot("dragonfly_windup");
                yield return Wait(0.3f);
                yield return Shot("dragonfly_dart");
                yield return Wait(1f);
            }

            // Cóc Tía
            var king = BossBase.Find("toadking");
            if (king != null)
            {
                Place(p, king.Home + new Vector2(-2.5f, -4.4f));
                yield return Wait(1.8f);
                yield return Shot("toadking_intro");
                yield return Wait(2.2f);
                king.DebugForce("tongue");
                yield return Wait(0.45f);
                yield return Shot("toadking_tongue_warning");
                yield return Wait(0.35f);
                yield return Shot("toadking_tongue");
                yield return Wait(1.2f);
                king.DebugForce("spit");
                yield return Wait(1.1f);
                yield return Shot("toadking_spit");
                yield return Wait(1.1f);
                yield return Shot("poison_pools");
                king.DebugForce("leap");
                yield return Wait(0.8f);
                yield return Shot("toadking_leap");
                yield return Wait(1.4f);
                king.health.TakeDamage(DamageInfo.Make(king.health.maxHp * 0.55f, Team.Player, p.gameObject, king.transform.position, Vector2.up));
                yield return Wait(1.8f);
                king.DebugForce("summon");
                yield return Wait(1.6f);
                yield return Shot("toadking_summon");
                king.health.Kill();
                yield return Wait(2.4f);
                yield return Shot("toadking_defeated");
                yield return Wait(1.5f);
            }

            // Xà Mẫu Đầm Lầy
            var snake = BossBase.Find("snake");
            if (snake != null)
            {
                Place(p, snake.Home + new Vector2(-7.4f, -0.8f));
                yield return Wait(1.8f);
                yield return Shot("snake_intro");
                yield return Wait(2.2f);
                snake.DebugForce("tail");
                yield return Wait(0.6f);
                yield return Shot("snake_tail_warning");
                yield return Wait(0.35f);
                yield return Shot("snake_tail");
                yield return Wait(1.2f);
                snake.DebugForce("venom");
                yield return Wait(0.6f);
                yield return Shot("snake_venom_warning");
                yield return Wait(0.45f);
                yield return Shot("snake_venom");
                yield return Wait(1.5f);
                snake.DebugForce("dive");
                yield return Wait(1.1f);
                yield return Shot("snake_dive");
                yield return Wait(2.2f);
                yield return Shot("snake_emerge");
                yield return Wait(1.5f);
                // enraged, it charges; a mound between it and the hero stops it cold
                snake.health.TakeDamage(DamageInfo.Make(snake.health.maxHp * 0.55f, Team.Player, p.gameObject, snake.transform.position, Vector2.up));
                yield return Wait(2f);
                snake.motor.Teleport(snake.Home);
                Place(p, snake.Home + new Vector2(-8.4f, 4.8f));
                yield return Wait(0.2f);
                snake.DebugForce("charge");
                yield return Wait(0.6f);
                yield return Shot("snake_charge_warning");
                yield return Wait(0.9f);
                yield return Shot("snake_crash");
                yield return Wait(3.2f);
                snake.DebugForce("summon");
                yield return Wait(1.6f);
                yield return Shot("snake_summon");
                snake.health.Kill();
                yield return Wait(2.4f);
                yield return Shot("snake_defeated");
                yield return Wait(1.5f);
            }

            // the swamp at night: the dragonflies are gone and Ma Trơi come out
            if (dn != null) dn.time = 0.02f;
            var haunt = Spot("wisps");
            Vector2 hauntAt = haunt != null ? (Vector2)haunt.position : new Vector2(147f, 40f);
            Place(p, hauntAt + new Vector2(-5.5f, -1.5f));
            WispAI wisp = null;
            for (float waited = 0f; waited < 10f && wisp == null; waited += 0.25f)
            {
                yield return Wait(0.25f);
                wisp = FindEnemy("wisp", hauntAt) as WispAI;
            }
            if (wisp != null)
            {
                yield return Wait(1.2f);
                yield return Shot("wisps_at_night");
                Place(p, (Vector2)wisp.transform.position + new Vector2(-3f, -0.6f));
                yield return Wait(1.6f);
                yield return Shot("wisp_lure");
                wisp.DebugSwell(p);
                yield return Wait(0.5f);
                yield return Shot("wisp_swell");
                yield return Wait(0.5f);
                yield return Shot("wisp_burst");
                yield return Wait(1f);
            }

            // home through the stones
            if (outpost != null)
            {
                Place(p, outpost.Arrival + new Vector2(0.6f, 0f));
                yield return Wait(2.5f);
                yield return Shot("swamp_night");
                if (p.waystones != null) p.waystones.RequestTravel("village");
                yield return Wait(1.2f);
                yield return Shot("travel_village");
            }
            if (dn != null) dn.time = 0.42f;
        }

        /// <summary>
        /// Hang Pha Lê: the trail up from the swamp and the mouth in the cliff, the miners' camp in the
        /// dark, the world map, bats darting, a spider's web, a golem's slam, the crystal forest and
        /// the spider queen's hall with its pillars.
        /// </summary>
        IEnumerator Cave(PlayerController p, DayNightCycle dn)
        {
            var zone = ZoneRoot.Current;
            Transform Spot(string id) => zone != null ? zone.SpotOf(id) : null;
            Vector2 At(string id, Vector2 fallback)
            {
                var t = Spot(id);
                return t != null ? (Vector2)t.position : fallback;
            }
            if (dn != null) dn.time = 0.45f;

            Place(p, new Vector2(168f, 57.5f));
            yield return Wait(1.6f);
            yield return Shot("cave_trail");
            var mouth = At("cavemouth", new Vector2(168f, 72.5f));
            Place(p, mouth + new Vector2(0.5f, -3f));
            yield return Wait(3.5f);   // the eyes adjust to the dark
            yield return Shot("cave_camp");
            foreach (var w in Waystone.All)
                if (p.waystones != null) p.waystones.Touch(w);
            yield return Wait(0.3f);
            if (WorldMapUI.I != null)
            {
                WorldMapUI.I.Show();
                yield return Wait(0.8f);
                yield return Shot("cave_world_map");
                WorldMapUI.I.Close();
                yield return Wait(0.3f);
            }

            // crystal bats: one darts through the hero
            var bats = At("batcave", new Vector2(146f, 77f));
            var bat = FindEnemy("bat", bats) as DragonflyAI;
            if (bat != null)
            {
                Place(p, (Vector2)bat.transform.position + new Vector2(-2.6f, -1f));
                yield return Wait(1.6f);
                yield return Shot("bats");
                bat.DebugDart(p);
                yield return Wait(0.3f);
                yield return Shot("bat_windup");
                yield return Wait(0.3f);
                yield return Shot("bat_dart");
                yield return Wait(1f);
            }

            // a cave spider spits web
            var nest = At("spidernest", new Vector2(157f, 106f));
            var spider = FindEnemy("spider", nest) as CaveSpiderAI;
            if (spider != null)
            {
                Place(p, (Vector2)spider.transform.position + new Vector2(-4f, -0.6f));
                yield return Wait(1.2f);
                spider.DebugWeb(p);
                yield return Wait(0.35f);
                yield return Shot("spider_web_warning");
                yield return Wait(0.45f);
                yield return Shot("spider_web");
                yield return Wait(1.2f);
            }

            // a golem in the old mine raises its fists
            var mine = At("mine", new Vector2(177f, 90f));
            var golem = FindEnemy("golem", mine);
            if (golem != null)
            {
                Place(p, (Vector2)golem.transform.position + new Vector2(-1.8f, -0.3f));
                yield return Wait(0.9f);
                yield return Shot("golem_windup");
                yield return Wait(0.8f);
                yield return Shot("golem_slam");
                yield return Wait(1f);
            }

            Place(p, At("crystalforest", new Vector2(136f, 95f)) + new Vector2(-2f, -3f));
            yield return Wait(2.5f);
            yield return Shot("crystal_forest");
            Place(p, At("queenhall", new Vector2(117f, 115f)) + new Vector2(0f, -6f));
            yield return Wait(2.5f);
            yield return Shot("queen_hall");
            Place(p, At("crystalhall", new Vector2(186f, 112f)) + new Vector2(-3f, -3f));
            yield return Wait(2.5f);
            yield return Shot("crystal_hall");
        }

        IEnumerator Run()
        {
            var gm = GameManager.I;
            var p = Players.Local;
            var dn = DayNightCycle.I;
            if (dn != null)
            {
                dn.time = 0.42f;
                dn.dayLength = 0f;
            }
            p.health.invulnerable = true;
            yield return Wait(2.5f);
            if (Only == "swamp")
            {
                yield return Swamp(p, dn);
                Finish();
                yield break;
            }
            if (Only == "cave")
            {
                yield return Cave(p, dn);
                Finish();
                yield break;
            }
            yield return Shot("village");
            HUD.I.help.Show();
            yield return Wait(0.6f);
            yield return Shot("help");
            HUD.I.help.Close();
            yield return Wait(0.3f);

            // --- talk to the chief
            var chief = NPC.All.Find(x => x.npcId == "chief");
            if (chief != null)
            {
                Place(p, chief.transform.position + new Vector3(-0.4f, -1.3f));
                yield return Wait(0.6f);
                chief.Interact(p);
                yield return Wait(2.2f);
                yield return Shot("dialogue");
                yield return FinishDialogue();
                yield return Wait(0.8f);
                yield return Shot("quest_started");
            }

            // --- Bé Mai offers the side quest with a choice
            var girl = NPC.All.Find(x => x.npcId == "girl");
            if (girl != null)
            {
                Place(p, girl.transform.position + new Vector3(-0.4f, -1.3f));
                yield return Wait(0.5f);
                girl.Interact(p);
                for (int i = 0; i < 20 && !DialogueUI.I.ShowingOptions; i++)
                {
                    DialogueUI.I.DebugAdvance();
                    yield return Wait(0.1f);
                }
                yield return Wait(0.4f);
                yield return Shot("dialogue_choice");
                yield return FinishDialogue();
                yield return Wait(0.5f);
            }

            // --- forest fight
            var slime = FindEnemy("slime", gm.forestSpot.position);
            if (slime != null)
            {
                Place(p, (Vector2)slime.transform.position + new Vector2(-2.6f, -0.6f));
                yield return Wait(0.8f);
                for (int k = 0; k < 3; k++)
                {
                    Prep(p);
                    p.skills.TryCast(0, slime.transform.position);
                    yield return Wait(k == 2 ? 0.12f : 0.33f);
                }
                yield return Shot("slash_combo");
                yield return Wait(0.5f);
            }
            var target = FindEnemy("shroom", p.transform.position) ?? FindEnemy("slime", p.transform.position);
            if (target != null)
            {
                Place(p, (Vector2)target.transform.position + new Vector2(-4.5f, -1f));
                yield return Wait(0.5f);
                Prep(p);
                p.skills.TryCast(1, target.transform.position);
                yield return Wait(0.2f);
                yield return Shot("fireball");
                yield return Wait(0.25f);
                yield return Shot("fire_explosion");
                yield return Wait(0.6f);
                Prep(p);
                p.skills.TryCast(2, (Vector2)p.transform.position + Vector2.right * 6f);
                yield return Wait(0.35f);
                yield return Shot("ice_spikes");
                yield return Wait(0.6f);
                Prep(p);
                p.skills.TryCast(3, (Vector2)p.transform.position + new Vector2(4.5f, 0.5f));
                yield return Wait(0.75f);
                yield return Shot("lightning_storm");
                yield return Wait(1.2f);
                Prep(p);
                p.skills.TryCast(6, p.transform.position);
                yield return Wait(0.5f);
                yield return Shot("blade_storm");
                Prep(p);
                p.skills.TryCast(5, p.transform.position);
                yield return Wait(p.ActionRemaining + 0.02f);   // Hồi Phục waits for the Khiên Thánh pose to end
                Prep(p);
                p.skills.TryCast(4, p.transform.position);
                yield return Wait(0.2f);
                yield return Shot("shield_heal");
                yield return Wait(2.5f);
            }

            // --- hover outline and death dissolve (Sprite Lit FX)
            var victim = FindEnemy("slime", p.transform.position) ?? FindEnemy("shroom", p.transform.position);
            if (victim != null && victim.style != null)
            {
                Place(p, (Vector2)victim.transform.position + new Vector2(-2.2f, -0.8f));
                yield return Wait(0.4f);
                victim.style.SetOutline(true, new Color(1.8f, 0.45f, 0.35f));
                yield return Wait(0.4f);
                yield return Shot("hover_outline");
                victim.style.SetOutline(false, Color.white);
                victim.health.Kill();
                yield return Wait(1.2f + 0.3f);
                yield return Shot("dissolve");
                yield return Wait(0.6f);
            }

            // --- UI panels
            HUD.I.inventory.Show();
            yield return Wait(0.6f);
            yield return Shot("inventory");
            HUD.I.inventory.Close();
            HUD.I.journal.Show();
            yield return Wait(0.6f);
            yield return Shot("journal");
            HUD.I.journal.Close();
            yield return Wait(0.4f);

            // --- level up, character sheet, save window (opened only, AutoShot never writes saves)
            if (p.stats != null)
            {
                p.stats.AddXp(p.stats.XpToNext + 20);
                yield return Wait(0.5f);
                yield return Shot("level_up");
                if (HUD.I.character != null)
                {
                    HUD.I.character.Show();
                    yield return Wait(0.6f);
                    yield return Shot("character");
                    HUD.I.character.Close();
                    yield return Wait(0.3f);
                }
            }
            if (HUD.I.saves != null)
            {
                HUD.I.saves.Open(true);
                yield return Wait(0.6f);
                yield return Shot("save_slots");
                HUD.I.saves.Close();
                yield return Wait(0.3f);
            }

            // --- boss
            var boss = BossBase.Find("bear");
            if (boss != null)
            {
                Place(p, (Vector2)gm.bossSpot.position + new Vector2(0.5f, -5.5f));
                yield return Wait(1.5f);
                yield return Shot("boss_intro");
                yield return Wait(2.5f);
                if (DebugConsole.I != null) DebugConsole.I.Execute("hitbox");
                boss.DebugForce("stomp");
                yield return Wait(0.65f);
                yield return Shot("stomp_warning");
                yield return Wait(0.45f);
                yield return Shot("stomp_impact");
                if (DebugConsole.I != null) DebugConsole.I.Execute("hitbox");
                yield return Wait(1.6f);
                boss.DebugForce("rock");
                yield return Wait(1.3f);
                yield return Shot("rock_throw");
                yield return Wait(1.6f);
                boss.DebugForce("pounce");
                yield return Wait(0.8f);
                yield return Shot("pounce");
                yield return Wait(1.5f);
                Prep(p);
                p.skills.TryCast(3, boss.transform.position);
                yield return Wait(0.9f);
                yield return Shot("storm_on_boss");
                if (boss.poise != null)
                {
                    var hit = DamageInfo.Make(1, Team.Player, p.gameObject, boss.transform.position, Vector2.up);
                    hit.poise = boss.poise.Threshold;
                    boss.health.TakeDamage(hit);
                    yield return Wait(0.5f);
                    yield return Shot("poise_break");
                    yield return Wait(1.5f);
                }
                boss.health.TakeDamage(DamageInfo.Make(boss.health.maxHp * 0.55f, Team.Player, p.gameObject, boss.transform.position, Vector2.up));
                yield return Wait(1.0f);
                yield return Shot("boss_enraged");
                yield return Wait(1.5f);
                boss.health.Kill();
                yield return Wait(2.2f);
                yield return Shot("boss_defeated");
                yield return Wait(2f);
            }

            yield return Swamp(p, dn);

            // --- night
            if (dn != null) dn.time = 0.02f;
            Place(p, gm.respawnPoint.position);
            yield return Wait(2f);
            yield return Shot("night_village");
            HUD.I.pause.Show();
            yield return Wait(0.5f);
            yield return Shot("pause");
            HUD.I.pause.Close();
            if (DebugConsole.I != null)
            {
                DebugConsole.I.Toggle();
                DebugConsole.I.Execute("stats");
                DebugConsole.I.Execute("help");
                yield return Wait(0.3f);
                yield return Shot("console");
                DebugConsole.I.Toggle();
            }
            yield return Wait(0.3f);
            p.health.invulnerable = false;
            p.health.Kill();
            yield return Wait(2.8f);
            yield return Shot("death");
            yield return Wait(0.5f);
            Finish();
        }
    }
}
