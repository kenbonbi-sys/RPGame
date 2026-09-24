using UnityEngine;

namespace RPG
{
    /// <summary>Hit-stop and slow motion. The only place that writes Time.timeScale.</summary>
    public class TimeFX : MonoBehaviour
    {
        static TimeFX I;
        float hitStopUntil;
        float slowUntil;
        float slowScale = 1f;
        public static bool Paused;

        void Awake()
        {
            I = this;
            Paused = false;
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (I == this) Time.timeScale = 1f;
        }

        /// <summary>Freezes the action for a few frames to sell a heavy hit.</summary>
        public static void HitStop(float seconds)
        {
            if (I == null) return;
            I.hitStopUntil = Mathf.Max(I.hitStopUntil, Time.unscaledTime + seconds);
        }

        public static void SlowMo(float scale, float seconds)
        {
            if (I == null) return;
            I.slowScale = scale;
            I.slowUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            float s = 1f;
            float now = Time.unscaledTime;
            if (Paused) s = 0f;
            else if (now < hitStopUntil) s = 0.03f;
            else if (now < slowUntil)
            {
                // ease back to normal speed during the last 30% of the slow-mo
                float remain = slowUntil - now;
                s = remain < 0.4f ? Mathf.Lerp(1f, slowScale, remain / 0.4f) : slowScale;
            }
            Time.timeScale = s;
        }
    }
}
