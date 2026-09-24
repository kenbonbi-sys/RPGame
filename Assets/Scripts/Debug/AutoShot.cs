using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Automated showcase used for testing builds: start the player with
    ///   Game.exe -autoshot -autoshotDir "C:\shots"
    /// It plays a scripted tour (dialogue, skills, boss attacks, night), saves screenshots and quits.
    /// Does nothing in normal play.
    /// </summary>
    public class AutoShot : MonoBehaviour
    {
        public static bool Active => Array.IndexOf(Environment.GetCommandLineArgs(), "-autoshot") >= 0;

        string dir;
        int index;

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
            StartCoroutine(Run());
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
            foreach (var e in FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude))
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

        IEnumerator Run()
        {
            var gm = GameManager.I;
            var p = gm.player;
            var dn = DayNightCycle.I;
            if (dn != null)
            {
                dn.time = 0.42f;
                dn.dayLength = 0f;
            }
            p.health.invulnerable = true;
            yield return Wait(2.5f);
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
                DialogueUI.I.SkipAll();
                yield return Wait(0.8f);
                yield return Shot("quest_started");
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
                p.skills.TryCast(4, p.transform.position);
                yield return Wait(0.5f);
                yield return Shot("shield_heal");
                yield return Wait(2.5f);
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
            if (PlayerStats.I != null)
            {
                PlayerStats.I.AddXp(PlayerStats.I.XpToNext + 20);
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
            var boss = FindAnyObjectByType<BossBear>();
            if (boss != null)
            {
                Place(p, (Vector2)gm.bossSpot.position + new Vector2(0.5f, -5.5f));
                yield return Wait(1.5f);
                yield return Shot("boss_intro");
                yield return Wait(2.5f);
                boss.DebugForce("stomp");
                yield return Wait(0.65f);
                yield return Shot("stomp_warning");
                yield return Wait(0.45f);
                yield return Shot("stomp_impact");
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

            // --- night
            if (dn != null) dn.time = 0.02f;
            Place(p, gm.respawnPoint.position);
            yield return Wait(2f);
            yield return Shot("night_village");
            HUD.I.pause.Show();
            yield return Wait(0.5f);
            yield return Shot("pause");
            HUD.I.pause.Close();
            yield return Wait(0.3f);
            p.health.invulnerable = false;
            p.health.Kill();
            yield return Wait(2.8f);
            yield return Shot("death");
            yield return Wait(0.5f);
            Application.Quit();
        }
    }
}
