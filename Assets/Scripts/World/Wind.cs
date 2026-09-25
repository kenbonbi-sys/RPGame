using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Gió của Thảo Nguyên Gió (plan §10, T61): the wind turns every <see cref="CycleSeconds"/>
    /// seconds to a new heading and blows in gusts (calm, rising, a long gust, easing off). It
    /// pushes whoever walks (<see cref="HeroPush"/> units a second at full strength) and bends
    /// every shot's flight (<see cref="ShotDrift"/>), but only inside the windy areas
    /// (<see cref="ZoneArea.windy"/>). It is worked out from the time of day, which the server
    /// shares with every player: every machine feels the same wind without a message.
    /// Presentation reads it too: the grass sways (the <c>RPG/Sprite Lit Wind</c> shader's globals),
    /// streaks of air fly by and the minimap shows its heading.
    /// </summary>
    public static class Wind
    {
        public const float CycleSeconds = 24f;
        /// <summary>Units a second a walker is pushed at full strength.</summary>
        public const float HeroPush = 1.5f;
        /// <summary>Units a second a shot drifts at full strength.</summary>
        public const float ShotDrift = 2.6f;
        /// <summary>The length of a day the wind's clock counts in (seconds), whatever the day cycle's speed.</summary>
        const float DaySeconds = 360f;

        static readonly List<ZoneArea> Windy = new List<ZoneArea>();
        static readonly int WindId = Shader.PropertyToID("_RpgWind");

        /// <summary>A test's or a tour's own wind (null: the real one).</summary>
        public static Vector2? Override;

        internal static void Register(ZoneArea z)
        {
            if (!Windy.Contains(z)) Windy.Add(z);
        }

        internal static void Unregister(ZoneArea z) => Windy.Remove(z);

        /// <summary>Seconds on the wind's clock: the shared time of day, or the game's own clock without one.</summary>
        public static float Clock => DayNightCycle.I != null ? DayNightCycle.I.time * DaySeconds : Time.time;

        /// <summary>Which cycle the wind is in (a new heading each).</summary>
        public static int Cycle => Mathf.FloorToInt(Clock / CycleSeconds);

        /// <summary>The heading of cycle <paramref name="k"/>, in degrees: a big turn from the one before, never a small drift.</summary>
        public static float HeadingOf(int k)
        {
            uint h = (uint)(k * 2654435761u) ^ 0x9E3779B9u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return Mathf.Repeat(k * 137.5f + (h % 60u), 360f);
        }

        /// <summary>How hard it blows through a cycle (0..1 of it): calm, rising, gusting, easing.</summary>
        public static float StrengthAt(float phase)
        {
            if (phase < 0.12f) return 0.15f;
            if (phase < 0.28f) return Mathf.Lerp(0.15f, 1f, Mathf.SmoothStep(0f, 1f, (phase - 0.12f) / 0.16f));
            if (phase < 0.82f) return 1f;
            return Mathf.Lerp(1f, 0.15f, Mathf.SmoothStep(0f, 1f, (phase - 0.82f) / 0.18f));
        }

        /// <summary>The wind now: its heading times its strength (length 0..1), wherever it blows.</summary>
        public static Vector2 Current
        {
            get
            {
                if (Override.HasValue) return Override.Value;
                float clock = Clock;
                int k = Mathf.FloorToInt(clock / CycleSeconds);
                float phase = clock / CycleSeconds - k;
                // a little flutter in the gust
                float s = StrengthAt(phase) * (0.92f + 0.08f * Mathf.Sin(clock * 2.3f));
                return Util.FromAngle(HeadingOf(k)) * s;
            }
        }

        /// <summary>How windy a point is, 0..1 (the windiest area around it).</summary>
        public static float WindinessAt(Vector2 pos)
        {
            float w = 0f;
            for (int i = 0; i < Windy.Count; i++)
            {
                var z = Windy[i];
                if (z != null && z.windy > w && z.Contains(pos)) w = z.windy;
            }
            return w;
        }

        /// <summary>The wind at a point (heading × strength × windiness; zero out of the windy areas).</summary>
        public static Vector2 At(Vector2 pos)
        {
            if (Windy.Count == 0) return Vector2.zero;
            float w = WindinessAt(pos);
            return w > 0f ? Current * w : Vector2.zero;
        }

        /// <summary>"Đông Bắc": where the wind blows from, as a sailor says it.</summary>
        public static string FromName(Vector2 wind)
        {
            if (wind.sqrMagnitude < 0.0001f) return "lặng";
            float a = Mathf.Repeat(Mathf.Atan2(-wind.y, -wind.x) * Mathf.Rad2Deg, 360f);
            string[] names = { "Đông", "Đông Bắc", "Bắc", "Tây Bắc", "Tây", "Tây Nam", "Nam", "Đông Nam" };
            return names[Mathf.RoundToInt(a / 45f) % 8];
        }

        /// <summary>An arrow pointing where the wind blows to.</summary>
        public static string Arrow(Vector2 wind)
        {
            if (wind.sqrMagnitude < 0.0001f) return "·";
            float a = Mathf.Repeat(Mathf.Atan2(wind.y, wind.x) * Mathf.Rad2Deg, 360f);
            string[] arrows = { "→", "↗", "↑", "↖", "←", "↙", "↓", "↘" };
            return arrows[Mathf.RoundToInt(a / 45f) % 8];
        }

        /// <summary>Hands the wind to the shaders (the grass sways with it), once a frame; the screens only.</summary>
        public static void Present()
        {
            var w = Current;
            Shader.SetGlobalVector(WindId, new Vector4(w.x, w.y, Clock, 1f));
        }
    }
}
