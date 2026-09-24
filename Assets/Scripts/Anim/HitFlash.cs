using UnityEngine;

namespace RPG
{
    /// <summary>
    /// White/colour flash on hit. Uses a child SpriteRenderer with an unlit silhouette
    /// material that mirrors the main sprite every frame, so it works with 2D lighting.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        public SpriteRenderer source;
        public SpriteRenderer overlay;
        public float duration = 0.12f;

        float t;
        Color color = Color.white;
        float strength = 1f;
        Color tint = Color.white;
        float tintAmount;

        public void Flash(Color c, float s = 1f, float dur = 0.12f)
        {
            color = c;
            strength = s;
            duration = dur;
            t = dur;
        }

        /// <summary>Persistent colour tint (e.g. blue while slowed, red while enraged).</summary>
        public void SetTint(Color c, float amount)
        {
            tint = c;
            tintAmount = amount;
        }

        void LateUpdate()
        {
            if (source == null || overlay == null) return;
            t -= Time.deltaTime;
            float a = t > 0 ? strength * Mathf.Clamp01(t / duration) : 0f;
            Color c;
            if (a > 0.01f) c = new Color(color.r, color.g, color.b, a);
            else if (tintAmount > 0.01f) c = new Color(tint.r, tint.g, tint.b, tintAmount);
            else
            {
                if (overlay.enabled) overlay.enabled = false;
                return;
            }
            overlay.enabled = true;
            overlay.sprite = source.sprite;
            overlay.flipX = source.flipX;
            overlay.color = c;
        }
    }
}
