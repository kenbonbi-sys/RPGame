using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RPG
{
    /// <summary>Scales a Light2D with the night factor (lanterns, campfire...) plus optional flicker.</summary>
    public class NightLight : MonoBehaviour
    {
        public Light2D target;
        public float dayIntensity = 0.25f;
        public float nightIntensity = 1.2f;
        public float flicker = 0.1f;
        public float flickerSpeed = 7f;
        /// <summary>Scales how far the light reaches (a hero with darkvision: wider).</summary>
        [System.NonSerialized] public float rangeScale = 1f;
        float seed, baseRange = -1f;

        void Awake()
        {
            if (target == null) target = GetComponent<Light2D>();
            if (target != null) baseRange = target.pointLightOuterRadius;
            seed = Random.value * 50f;
        }

        void Update()
        {
            if (target == null) return;
            float n = DayNightCycle.Darkness;   // at night, and under the rock
            float f = 1f + (Mathf.PerlinNoise(seed, Time.time * flickerSpeed) - 0.5f) * 2f * flicker;
            target.intensity = Mathf.Lerp(dayIntensity, nightIntensity, n) * f;
            if (baseRange > 0f) target.pointLightOuterRadius = baseRange * rangeScale;
        }
    }
}
