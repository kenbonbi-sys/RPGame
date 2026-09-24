using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Full-screen feedback: colour flashes (UI overlay) and a post-processing
    /// "impact" volume (chromatic aberration + lens distortion) that pulses on big hits.
    /// </summary>
    public class ScreenFX : MonoBehaviour
    {
        static ScreenFX I;

        public Image flashImage;
        public Volume impactVolume;
        public Volume dangerVolume;   // red vignette when the player is low on HP

        float flashAlpha, flashDecay = 4f;
        float impact, impactDecay = 3f;
        float danger;

        void Awake()
        {
            I = this;
            if (flashImage != null) flashImage.color = new Color(1, 1, 1, 0);
        }

        public static void Flash(Color c, float strength = 0.6f, float duration = 0.25f)
        {
            if (I == null || I.flashImage == null) return;
            I.flashImage.color = new Color(c.r, c.g, c.b, I.flashImage.color.a);
            I.flashAlpha = Mathf.Max(I.flashAlpha, strength);
            I.flashDecay = strength / Mathf.Max(0.05f, duration);
        }

        public static void Impact(float strength = 1f, float duration = 0.35f)
        {
            if (I == null) return;
            I.impact = Mathf.Max(I.impact, Mathf.Clamp01(strength));
            I.impactDecay = 1f / Mathf.Max(0.05f, duration);
        }

        public static void SetDanger(float t)
        {
            if (I != null) I.danger = Mathf.Clamp01(t);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (flashImage != null)
            {
                flashAlpha = Mathf.Max(0, flashAlpha - flashDecay * dt);
                var c = flashImage.color;
                c.a = flashAlpha;
                flashImage.color = c;
                flashImage.enabled = flashAlpha > 0.003f;
            }
            impact = Mathf.Max(0, impact - impactDecay * dt);
            if (impactVolume != null) impactVolume.weight = impact;
            if (dangerVolume != null)
            {
                float pulse = danger > 0 ? danger * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f)) : 0f;
                dangerVolume.weight = Mathf.MoveTowards(dangerVolume.weight, pulse, dt * 2f);
            }
        }
    }
}
