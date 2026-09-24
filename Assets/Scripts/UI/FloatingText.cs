using TMPro;
using UnityEngine;

namespace RPG
{
    public class FloatingText : MonoBehaviour
    {
        public enum Style { Normal, Crit, Rise, Status }

        public TextMeshProUGUI text;
        public float lifetime = 0.9f;

        FloatingTextManager owner;
        Vector3 world;
        Vector2 drift;
        float t;
        float scale;
        Style style;
        Color color;

        public void Play(FloatingTextManager m, string s, Vector3 w, Color c, float sc, Style st)
        {
            owner = m;
            world = w;
            color = c;
            scale = sc;
            style = st;
            t = 0;
            text.text = s;
            text.color = c;
            drift = st == Style.Normal || st == Style.Crit ? new Vector2(Random.Range(-40f, 40f), 0) : Vector2.zero;
            lifetime = st == Style.Status ? 1.3f : st == Style.Crit ? 1.1f : 0.85f;
            Update();
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / lifetime);
            var rt = (RectTransform)transform;
            if (!UIUtil.WorldToLayer(world, owner != null ? owner.layer : (RectTransform)rt.parent, out Vector2 local))
            {
                text.enabled = false;
            }
            else
            {
                text.enabled = true;
                float rise = style == Style.Status ? 40f * Util.EaseOutCubic(k) : 70f * Util.EaseOutCubic(k);
                float arcX = drift.x * Util.EaseOutCubic(k);
                rt.anchoredPosition = local + new Vector2(arcX, 30f + rise);
            }
            // pop in, settle, fade
            float pop = k < 0.12f ? Mathf.Lerp(1.9f, 1f, k / 0.12f) : 1f;
            if (style == Style.Crit && k < 0.3f) pop *= 1f + 0.08f * Mathf.Sin(t * 60f);
            rt.localScale = Vector3.one * (scale * pop);
            float a = k > 0.65f ? 1f - (k - 0.65f) / 0.35f : 1f;
            text.color = new Color(color.r, color.g, color.b, a);
            if (t >= lifetime && owner != null) owner.Recycle(this);
        }
    }
}
