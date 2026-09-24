using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Bù trễ cho Lướt (Docs/KeHoach-Online.md §4, §11). A player's dash reaches the server half a
    /// ping after they pressed it, so where the world's rules run an enemy's hit on a hero played
    /// from another machine waits that long before it lands. A dash pressed in time has made the
    /// hero invulnerable by then and the hit is dodged (a Lướt Hoàn Hảo too), as it was on the
    /// player's screen: the server leans toward the dodger. Damage over time, scripted hits and
    /// hits on the heroes played on this machine land at once, as offline.
    /// </summary>
    public static class LagCompensation
    {
        /// <summary>The longest a hit waits: a player with a worse ping has to dodge a little earlier.</summary>
        public const float MaxHold = 0.15f;

        /// <summary>Seconds hits on a hero wait (half its player's ping, as their machine reports it). Tests replace it.</summary>
        public static Func<Health, float> HoldOf = DefaultHold;

        struct Held
        {
            public Health target;
            public DamageInfo hit;
            public float landsAt;
        }

        static readonly List<Held> Pending = new List<Held>();

        /// <summary>Hits on their way.</summary>
        public static int PendingCount => Pending.Count;

        /// <summary>
        /// <see cref="Health.TakeDamage"/> asks first: true when the hit was held and lands
        /// later (<see cref="Tick"/>), with the invulnerability the hero has then.
        /// </summary>
        public static bool TryHold(Health target, DamageInfo d)
        {
            if (d.held || d.dot || d.pure || d.sourceTeam != Team.Enemy || target == null || target.team != Team.Player) return false;
            if (!GameSession.Serving || HoldOf == null) return false;
            float hold = Mathf.Min(HoldOf(target), MaxHold);
            if (hold < 0.01f) return false;
            d.held = true;
            Pending.Add(new Held { target = target, hit = d, landsAt = Time.time + hold });
            return true;
        }

        /// <summary>Lands the hits whose time has come (the server, every frame).</summary>
        public static void Tick() => Tick(Time.time);

        public static void Tick(float now)
        {
            for (int i = 0; i < Pending.Count; i++)
            {
                var h = Pending[i];
                if (h.landsAt > now) continue;
                Pending.RemoveAt(i--);
                if (h.target == null || !h.target.gameObject.activeInHierarchy) continue;
                if (h.target.TakeDamage(h.hit) > 0f && h.hit.feedback) Combat.OnHitFeedback(h.target, h.hit);
            }
        }

        /// <summary>Drops every hit on its way (the session ended).</summary>
        public static void Clear() => Pending.Clear();

        static float DefaultHold(Health target)
        {
            var hero = target.GetComponent<PlayerController>();
            var s = hero != null ? ServerPlayers.SessionOf(hero) : null;
            return s == null || s.Local ? 0f : s.pingMs / 2000f;
        }
    }
}
