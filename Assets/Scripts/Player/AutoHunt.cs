using System;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Tự Động (T, or the button above the skill bar): the local hero hunts on its own while its
    /// player looks away, through the same intents as the mouse and keyboard, so online the server
    /// sees honest walking and casts. It fights the enemy nearest to it around the spot where it was
    /// switched on (never a boss or anything near a boss's arena), uses its skills when they are
    /// ready and it can afford them, heals, shields and drinks when low, and walks back to that spot
    /// when nothing is left around. Walking by hand, a fall or T again switch it off; skills and
    /// potions pressed by hand still go through.
    /// </summary>
    public class AutoHunt
    {
        /// <summary>How far from the spot where it was switched on the hero goes hunting.</summary>
        public const float Leash = 11f;
        /// <summary>How far the hero looks for its next enemy.</summary>
        const float Sight = 9f;
        /// <summary>Enemies this close to a boss's home are left alone (a boss is no one's autopilot).</summary>
        const float BossBerth = 16f;

        public bool On { get; private set; }
        /// <summary>Where it was switched on: the middle of the hunting ground.</summary>
        public Vector2 Anchor { get; private set; }

        /// <summary>Raised when the local hero's Tự Động goes on or off (the HUD's button).</summary>
        public static event Action<bool> Changed;

        EnemyBase target;
        float nextSkillAt, nextPotionAt;
        int nextSlot = 1;

        public void Set(PlayerController pc, bool on, string why = null)
        {
            if (on == On) return;
            On = on;
            target = null;
            if (on) Anchor = pc.transform.position;
            if (pc.IsLocal)
            {
                string text = on ? "Tự động: BẬT" : "Tự động: TẮT" + (string.IsNullOrEmpty(why) ? "" : $" ({why})");
                Notify.WorldText(pc, text, pc.health.HeadPosition + Vector3.up * 0.5f, on ? Palette.Energy : new Color(0.8f, 0.8f, 0.8f));
                Notify.Sound(pc, on ? "sfx_ui_open" : "sfx_ui_close", 0.6f);
                Changed?.Invoke(on);
            }
        }

        /// <summary>The intent of a hero on Tự Động this frame; <paramref name="i"/> already holds what the player pressed by hand.</summary>
        public PlayerIntent Think(PlayerController pc, PlayerIntent i)
        {
            Vector2 at = pc.transform.position;
            float now = Time.time;
            if (target == null || !Huntable(target) || Vector2.Distance(target.transform.position, Anchor) > Leash + 3f)
                target = Pick(at);

            // low: drink, heal, shield
            if (pc.health.Fraction < 0.3f && now >= nextPotionAt && pc.PotionReadyIn <= 0f)
            {
                nextPotionAt = now + 3f;
                i.potionPresses |= 1;
            }
            if (i.skillPresses == 0 && now >= nextSkillAt)
            {
                int s = Support(pc, target != null);
                if (s >= 0)
                {
                    i.skillPresses |= 1 << s;
                    nextSkillAt = now + 0.4f;
                }
            }

            if (target == null)
            {
                // nothing left around: back to the middle of the hunting ground
                Vector2 home = Anchor - at;
                i.aim = Anchor;
                if (home.magnitude > 1.2f) i.move = home.normalized;
                return i;
            }

            Vector2 to = (Vector2)target.transform.position - at;
            i.aim = target.transform.position;
            float reach = PlayerController.Reach(at, target.health);
            // a skill in range now and then, cycling through the bar
            if (i.skillPresses == 0 && now >= nextSkillAt && !pc.IsActing)
            {
                int s = Offensive(pc, to.magnitude);
                if (s >= 0)
                {
                    i.skillPresses |= 1 << s;
                    nextSkillAt = now + 0.5f;
                }
            }
            if (reach > pc.basicAttackRange * 0.7f)
            {
                i.move = to.normalized;
            }
            else
            {
                i.face = to.normalized;
                if (!pc.IsActing)
                {
                    i.basicAttack = true;
                    i.basicAttackAt = target.transform.position;
                }
            }
            return i;
        }

        /// <summary>A heal when hurt, a shield when hurt and fighting; -1 when none is wanted or ready.</summary>
        static int Support(PlayerController pc, bool fighting)
        {
            float hp = pc.health.Fraction;
            var sk = pc.skills;
            for (int s = 1; s < 7; s++)
            {
                var a = sk.slots[s];
                if (a == null || !a.HasTag(AbilityTags.Support) || sk.ReadyIn(s) > 0f || !sk.CanAfford(s)) continue;
                bool heals = a.effects.Exists(e => e is HealEffect);
                if (heals && hp < 0.55f) return s;
                if (!heals && fighting && hp < 0.45f) return s;
            }
            return -1;
        }

        /// <summary>The next attack skill of slots 1–6 that is ready, affordable and reaches <paramref name="distance"/>; -1 when none.</summary>
        int Offensive(PlayerController pc, float distance)
        {
            var sk = pc.skills;
            for (int n = 0; n < 6; n++)
            {
                int s = 1 + (nextSlot - 1 + n) % 6;
                var a = sk.slots[s];
                if (a == null || a.HasTag(AbilityTags.Support) || a.HasTag(AbilityTags.Movement)) continue;
                if (sk.ReadyIn(s) > 0f || !sk.CanAfford(s)) continue;
                // keep a little energy for a heal
                if (pc.energy - a.energyCost < 15f && pc.health.Fraction < 0.6f) continue;
                float range = a.targeting == AbilityTargeting.Self ? 2.5f : Mathf.Min(a.maxRange, 9f);
                if (distance > range) continue;
                nextSlot = s % 6 + 1;
                return s;
            }
            return -1;
        }

        EnemyBase Pick(Vector2 from)
        {
            EnemyBase best = null;
            float bd = Sight * Sight;
            foreach (var e in EnemyBase.All)
            {
                if (!Huntable(e)) continue;
                Vector2 p = e.transform.position;
                if (Vector2.Distance(p, Anchor) > Leash) continue;
                float d = (p - from).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    best = e;
                }
            }
            return best;
        }

        static bool Huntable(EnemyBase e)
        {
            if (e == null || e.IsDead || !e.gameObject.activeInHierarchy || e.health == null) return false;
            if (!PlayerController.HasBody(e.health)) return false;   // under water, faded away
            Vector2 p = e.transform.position;
            foreach (var b in BossBase.All)
                if (b != null && Vector2.Distance(p, b.Home) < BossBerth) return false;
            return true;
        }
    }
}
