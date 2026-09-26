using UnityEngine;

namespace RPG
{
    /// <summary>Jagged, flickering lightning drawn with LineRenderers (core + glow): from the sky down to it, or between two points (<see cref="Between"/>).</summary>
    public class LightningBolt : MonoBehaviour
    {
        public LineRenderer core;
        public LineRenderer glow;
        public int segments = 12;
        public float jitter = 0.35f;
        public float lifetime = 0.35f;
        public float height = 9f;
        public float refreshRate = 0.04f;
        public Gradient fade;

        float t, nextRefresh;
        Vector3 start, end;

        void OnEnable()
        {
            t = 0;
            nextRefresh = 0;
            end = transform.position;
            start = end + new Vector3(Random.Range(-1.2f, 1.2f), height, 0);
            Build();
        }

        /// <summary>Draws it from <paramref name="a"/> to <paramref name="b"/> instead of from the sky (a chain of lightning leaping between foes).</summary>
        public void Between(Vector3 a, Vector3 b)
        {
            start = a;
            end = b;
            Build();
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t >= nextRefresh)
            {
                nextRefresh = t + refreshRate;
                Build();
            }
            float k = Mathf.Clamp01(t / lifetime);
            float a = 1f - k * k;
            SetAlpha(core, a);
            SetAlpha(glow, a * 0.6f);
        }

        void Build()
        {
            if (core == null) return;
            core.positionCount = segments + 1;
            if (glow != null) glow.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float f = i / (float)segments;
                Vector3 p = Vector3.Lerp(start, end, f);
                if (i > 0 && i < segments)
                    p += new Vector3(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter) * 0.4f, 0) * (1f + Mathf.Sin(f * Mathf.PI));
                core.SetPosition(i, p);
                if (glow != null) glow.SetPosition(i, p);
            }
        }

        static void SetAlpha(LineRenderer lr, float a)
        {
            if (lr == null) return;
            var c0 = lr.startColor;
            var c1 = lr.endColor;
            c0.a = a;
            c1.a = a;
            lr.startColor = c0;
            lr.endColor = c1;
        }
    }
}
