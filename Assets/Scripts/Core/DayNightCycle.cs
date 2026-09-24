using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RPG
{
    /// <summary>
    /// Drives the global 2D light through dawn / day / dusk / night.
    /// Other lights (campfire, lanterns, fireflies) read <see cref="NightFactor"/>.
    /// </summary>
    public class DayNightCycle : MonoBehaviour, ISaveable
    {
        public static DayNightCycle I { get; private set; }

        public Light2D globalLight;
        [Tooltip("Real seconds for a full day.")]
        public float dayLength = 300f;
        [Range(0, 1)] public float time = 0.3f; // 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset

        [Header("Light")]
        public Color nightColor = new Color(0.36f, 0.42f, 0.72f);
        public Color dawnColor = new Color(1f, 0.78f, 0.66f);
        public Color dayColor = new Color(1f, 0.98f, 0.94f);
        public Color duskColor = new Color(1f, 0.64f, 0.5f);
        public float nightIntensity = 0.42f;
        public float dayIntensity = 1f;

        /// <summary>0 during the day, 1 at deep night.</summary>
        public static float NightFactor => I != null ? I.night : 0f;
        public static bool IsNight => NightFactor > 0.5f;

        float night;

        void Awake()
        {
            I = this;
            SaveRegistry.Register(this);
        }

        void OnDestroy() => SaveRegistry.Unregister(this);

        [System.Serializable]
        class SaveState
        {
            public float time;
        }

        public string SaveKey => "time";
        public string CaptureState() => JsonUtility.ToJson(new SaveState { time = time });
        public void RestoreState(string json) => time = Mathf.Repeat(JsonUtility.FromJson<SaveState>(json).time, 1f);

        void Update()
        {
            if (dayLength > 0) time = Mathf.Repeat(time + Time.deltaTime / dayLength, 1f);
            Evaluate(out Color c, out float intensity, out night);
            if (globalLight != null)
            {
                globalLight.color = c;
                globalLight.intensity = intensity;
            }
        }

        void Evaluate(out Color c, out float intensity, out float nightF)
        {
            // key points: 0.0 night, 0.22 night, 0.28 dawn, 0.36 day, 0.66 day, 0.74 dusk, 0.8 night, 1.0 night
            float t = time;
            if (t < 0.22f) { c = nightColor; nightF = 1; }
            else if (t < 0.28f) { float k = (t - 0.22f) / 0.06f; c = Color.Lerp(nightColor, dawnColor, k); nightF = 1 - k * 0.7f; }
            else if (t < 0.36f) { float k = (t - 0.28f) / 0.08f; c = Color.Lerp(dawnColor, dayColor, k); nightF = 0.3f * (1 - k); }
            else if (t < 0.66f) { c = dayColor; nightF = 0; }
            else if (t < 0.74f) { float k = (t - 0.66f) / 0.08f; c = Color.Lerp(dayColor, duskColor, k); nightF = 0.3f * k; }
            else if (t < 0.8f) { float k = (t - 0.74f) / 0.06f; c = Color.Lerp(duskColor, nightColor, k); nightF = 0.3f + 0.7f * k; }
            else { c = nightColor; nightF = 1; }
            intensity = Mathf.Lerp(dayIntensity, nightIntensity, nightF);
        }

        /// <summary>Phase label and progress through that phase, e.g. ("Ngày", 0.24).</summary>
        public void GetPhase(out string label, out float progress, out bool isDay)
        {
            float t = time;
            if (t >= 0.25f && t < 0.75f)
            {
                isDay = true;
                progress = (t - 0.25f) / 0.5f;
                label = t < 0.33f ? "Bình minh" : (t > 0.68f ? "Hoàng hôn" : "Ngày");
            }
            else
            {
                isDay = false;
                float nt = t >= 0.75f ? t - 0.75f : t + 0.25f;
                progress = nt / 0.5f;
                label = "Đêm";
            }
        }
    }

}
