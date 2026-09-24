using System;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Lobbed projectile (the boss' boulder). Travels from A to B along a parabola:
    /// the root moves on the ground (with a shadow) and the visual child is lifted.
    /// </summary>
    public class ArcProjectile : MonoBehaviour
    {
        public Transform visual;
        public SpriteRenderer shadow;
        public float height = 3.5f;
        public float spin = 360f;

        Vector2 from, to;
        float duration, t;
        Action<Vector2> onLand;
        bool flying;

        public void Launch(Vector2 start, Vector2 end, float time, Action<Vector2> landed)
        {
            from = start;
            to = end;
            duration = Mathf.Max(0.1f, time);
            t = 0;
            onLand = landed;
            flying = true;
            transform.position = start;
        }

        void Update()
        {
            if (!flying) return;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.position = Vector2.Lerp(from, to, k);
            float h = 4f * height * k * (1 - k);
            if (visual != null)
            {
                visual.localPosition = new Vector3(0, h, 0);
                visual.Rotate(0, 0, -spin * Time.deltaTime);
            }
            if (shadow != null)
            {
                float s = Mathf.Lerp(0.6f, 1.2f, 1 - h / Mathf.Max(0.01f, height));
                shadow.transform.localScale = new Vector3(s, s, 1);
            }
            if (k >= 1f)
            {
                flying = false;
                var cb = onLand;
                onLand = null;
                cb?.Invoke(to);
                VFX.Release(gameObject);
            }
        }
    }
}
