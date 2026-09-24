using UnityEngine;

namespace RPG
{
    /// <summary>
    /// A straight beam of light between two points, drawn with LineRenderers (a white core in a
    /// coloured glow) that flare up and thin out: Mắt Hang's ray, the spider queen's Tia Pha Lê.
    /// Shown through <see cref="NetCues.Beam"/>, a bounced beam as two of them.
    /// </summary>
    public class BeamFX : MonoBehaviour
    {
        public LineRenderer core;
        public LineRenderer glow;
        [Tooltip("The glow's width over the beam's width.")]
        public float glowWidth = 2.6f;
        [Tooltip("Sparkles at the far end (optional).")]
        public ParticleSystem end;

        Vector2 from, to;
        float width, duration, t;
        Color color;

        /// <summary>Draws a beam on this screen (nothing on a server without one).</summary>
        public static void Show(Vector2 from, Vector2 to, float width, float duration, Color color)
        {
            var go = VFX.Spawn("crystal_beam", from, Quaternion.identity, 1f, null, true);
            var b = go != null ? go.GetComponent<BeamFX>() : null;
            if (b == null)
            {
                if (go != null) VFX.Release(go);
                return;
            }
            b.Set(from, to, width, duration, color);
            VFX.Spawn("crystal_burst", to, Quaternion.identity, 0.6f);
        }

        public void Set(Vector2 a, Vector2 b, float w, float seconds, Color c)
        {
            from = a;
            to = b;
            width = Mathf.Max(0.05f, w);
            duration = Mathf.Max(0.05f, seconds);
            color = c;
            t = 0f;
            transform.position = a;
            if (end != null) end.transform.position = b;
            foreach (var lr in new[] { core, glow })
            {
                if (lr == null) continue;
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(a.x, a.y, 0f));
                lr.SetPosition(1, new Vector3(b.x, b.y, 0f));
            }
            Draw(0f);
        }

        void Update()
        {
            t += Time.deltaTime;
            Draw(t / duration);
            if (t >= duration) VFX.Release(gameObject);
        }

        /// <summary>A flare as it fires, then a steady beam that thins out at the end.</summary>
        void Draw(float k)
        {
            k = Mathf.Clamp01(k);
            float flare = k < 0.12f ? Mathf.Lerp(1.6f, 1f, k / 0.12f) : 1f;
            float fade = k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f;
            if (core != null)
            {
                core.widthMultiplier = width * 0.45f * flare * Mathf.Lerp(0.3f, 1f, fade);
                core.startColor = core.endColor = new Color(1f, 1f, 1f, fade);
            }
            if (glow != null)
            {
                glow.widthMultiplier = width * glowWidth * 0.5f * flare * Mathf.Lerp(0.5f, 1f, fade);
                glow.startColor = glow.endColor = new Color(color.r, color.g, color.b, color.a * 0.75f * fade);
            }
        }
    }
}
