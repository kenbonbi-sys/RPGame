using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    public static class Util
    {
        /// <summary>Returns "down", "up" or "side" and whether the side sprite must be flipped (facing left).</summary>
        public static string Dir4(Vector2 v, out bool flipX)
        {
            flipX = false;
            if (v.sqrMagnitude < 0.0001f) return "down";
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y) * 1.05f)
            {
                flipX = v.x < 0;
                return "side";
            }
            return v.y > 0 ? "up" : "down";
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        public static Vector2 FromAngle(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        public static float EaseInCubic(float t) => Mathf.Pow(Mathf.Clamp01(t), 3f);
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
        public static float EaseInOut(float t) => t * t * (3f - 2f * Mathf.Clamp01(t));

        public static Vector2 RandomInCircle(float r) => Random.insideUnitCircle * r;

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static Color HDR(Color c, float intensity) => new Color(c.r * intensity, c.g * intensity, c.b * intensity, c.a);

        public static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static readonly List<Collider2D> Hits = new List<Collider2D>(64);
        static readonly HashSet<Health> Seen = new HashSet<Health>();

        /// <summary>
        /// All distinct Health components hit by a circle that an attacker of <paramref name="team"/> is allowed to damage.
        /// </summary>
        public static List<Health> HealthsInCircle(Vector2 center, float radius, Team team, List<Health> results)
        {
            results.Clear();
            Seen.Clear();
            var filter = new ContactFilter2D();
            filter.SetLayerMask(Layers.HittableMask);
            filter.useTriggers = true;
            Hits.Clear();
            Physics2D.OverlapCircle(center, radius, filter, Hits);
            foreach (var c in Hits)
            {
                var h = c.GetComponentInParent<Health>();
                if (h == null || h.IsDead || !h.CanBeDamagedBy(team) || !Seen.Add(h)) continue;
                results.Add(h);
            }
            return results;
        }

        /// <summary>Filters a circle query down to a cone (angle in degrees, total).</summary>
        public static List<Health> HealthsInCone(Vector2 origin, Vector2 dir, float radius, float angle, Team team, List<Health> results)
        {
            HealthsInCircle(origin, radius, team, results);
            for (int i = results.Count - 1; i >= 0; i--)
            {
                Vector2 to = (Vector2)results[i].transform.position - origin;
                if (to.sqrMagnitude < 0.36f) continue; // touching: always hit
                if (Vector2.Angle(dir, to) > angle * 0.5f) results.RemoveAt(i);
            }
            return results;
        }

        public static bool LineBlocked(Vector2 a, Vector2 b)
        {
            var hit = Physics2D.Linecast(a, b, Layers.ObstacleMask);
            return hit.collider != null && hit.collider.GetComponentInParent<Health>() == null;
        }

        public static string FormatSeconds(float s) => s >= 10f ? Mathf.CeilToInt(s).ToString() : s.ToString("0.0") + "s";
    }
}
