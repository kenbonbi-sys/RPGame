using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RPG
{
    /// <summary>Animates a Light2D intensity/radius over time (explosion flashes, spell glows).</summary>
    public class LightPulse : MonoBehaviour
    {
        public Light2D target;
        public float duration = 0.4f;
        public float peakIntensity = 2.5f;
        public AnimationCurve curve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        public bool loop;
        public float flicker;

        float t;
        float seed;

        void Awake()
        {
            if (target == null) target = GetComponent<Light2D>();
            seed = Random.value * 10f;
        }

        void OnEnable() => t = 0;

        void Update()
        {
            if (target == null) return;
            t += Time.deltaTime;
            float k = duration > 0 ? t / duration : 1f;
            if (loop) k = Mathf.Repeat(k, 1f);
            float f = flicker > 0 ? 1f + (Mathf.PerlinNoise(seed, Time.time * 12f) - 0.5f) * 2f * flicker : 1f;
            target.intensity = peakIntensity * curve.Evaluate(Mathf.Clamp01(k)) * f;
        }
    }
}
