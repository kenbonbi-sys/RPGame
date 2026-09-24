using TMPro;
using UnityEngine;

namespace RPG
{
    /// <summary>"Kỹ năng: Dậm Đất" — a boxed label that pops above a boss when it casts.</summary>
    public class SkillBannerUI : MonoBehaviour
    {
        public TextMeshProUGUI text;
        public CanvasGroup group;
        public float duration = 1.7f;

        Health owner;
        float t;
        RectTransform layer;

        public void Show(Health h, string s)
        {
            owner = h;
            t = 0;
            layer = (RectTransform)transform.parent;
            if (text != null) text.text = s;
            // only one banner per owner at a time
            foreach (var other in layer.GetComponentsInChildren<SkillBannerUI>())
                if (other != this && other.owner == h) other.Release();
            LateUpdate();
        }

        void Release()
        {
            owner = null;
            Pool.Release(gameObject, true);
        }

        void LateUpdate()
        {
            t += Time.unscaledDeltaTime;
            if (owner == null || t > duration)
            {
                Release();
                return;
            }
            bool vis = UIUtil.WorldToLayer(owner.HeadPosition + Vector3.up * 0.25f, layer, out Vector2 local);
            local += new Vector2(0, 14f * Util.EaseOutCubic(Mathf.Clamp01(t / 0.3f)));
            // keep clear of the boss bar at the top of the screen
            float maxY = layer.rect.yMax - 175f;
            if (local.y > maxY) local.y = maxY;
            ((RectTransform)transform).anchoredPosition = local;
            float a = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((duration - t) / 0.35f);
            if (group != null) group.alpha = vis ? a : 0;
            float s = t < 0.15f ? Mathf.Lerp(1.4f, 1f, t / 0.15f) : 1f;
            transform.localScale = Vector3.one * s;
        }
    }
}
