using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Every hero in the world, and the one this machine shows and controls
    /// (Docs/KeHoach-Online.md). Gameplay asks here instead of assuming a single hero: enemies
    /// pick targets among <see cref="All"/>, while the HUD, camera and input follow
    /// <see cref="Local"/>. Offline there is one hero and it is the local one.
    /// </summary>
    public static class Players
    {
        static readonly List<PlayerController> all = new List<PlayerController>();

        /// <summary>Heroes active in the world, dead ones included.</summary>
        public static IReadOnlyList<PlayerController> All => all;

        /// <summary>The hero this machine shows and controls; null on a zone server.</summary>
        public static PlayerController Local { get; private set; }

        public static void Register(PlayerController p)
        {
            if (p != null && !all.Contains(p)) all.Add(p);
        }

        public static void Unregister(PlayerController p) => all.Remove(p);

        public static void SetLocal(PlayerController p) => Local = p;

        /// <summary>Forgets everything (the game scene boots; keeps domain-reload-off safe).</summary>
        public static void Reset()
        {
            all.Clear();
            Local = null;
        }

        /// <summary>The closest living hero nearer than <paramref name="maxDistance"/>, or null.</summary>
        public static PlayerController Nearest(Vector2 at, float maxDistance = float.MaxValue)
        {
            PlayerController best = null;
            float bestSq = maxDistance * maxDistance;
            foreach (var p in all)
            {
                if (p == null || p.IsDead) continue;
                float d = ((Vector2)p.transform.position - at).sqrMagnitude;
                if (d < bestSq)
                {
                    bestSq = d;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>True while a living hero is nearer than <paramref name="radius"/>.</summary>
        public static bool AnyWithin(Vector2 at, float radius) => Nearest(at, radius) != null;

        /// <summary>Fills <paramref name="results"/> with the living heroes nearer than <paramref name="radius"/>.</summary>
        public static void Within(Vector2 at, float radius, List<PlayerController> results)
        {
            results.Clear();
            float sq = radius * radius;
            foreach (var p in all)
                if (p != null && !p.IsDead && ((Vector2)p.transform.position - at).sqrMagnitude < sq) results.Add(p);
        }
    }
}
