using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Hit-stop and slow motion; the only place that writes Time.timeScale. Offline the game owns
    /// its clock (<see cref="GameSession.OwnsTime"/>) and they stop or slow the whole world. In a
    /// shared world time runs on for everyone: a hit-stop holds the animation of the characters in
    /// the hit instead, and slow motion is left out (the rewards that come with it stay).
    /// </summary>
    public class TimeFX : MonoBehaviour
    {
        static TimeFX I;
        float hitStopUntil;
        float slowUntil;
        float slowScale = 1f;
        float slowEase = 0.4f;
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

        /// <summary>
        /// Freezes the action for a few frames to sell a heavy hit. In a shared world only the
        /// characters in the hit (<paramref name="a"/>, <paramref name="b"/>) hold their pose.
        /// </summary>
        public static void HitStop(float seconds, GameObject a = null, GameObject b = null)
        {
            if (!GameSession.OwnsTime)
            {
                Hold(a, seconds);
                Hold(b, seconds);
                return;
            }
            if (I == null) return;
            I.hitStopUntil = Mathf.Max(I.hitStopUntil, Time.unscaledTime + seconds);
        }

        static void Hold(GameObject who, float seconds)
        {
            var anim = who != null ? who.GetComponentInChildren<SpriteAnimator>() : null;
            if (anim != null) anim.Hold(seconds);
        }

        /// <summary>
        /// Time runs at <paramref name="scale"/> for <paramref name="seconds"/> of real time, easing
        /// back to full speed over the last <paramref name="easeOut"/> of them. Offline only.
        /// </summary>
        public static void SlowMo(float scale, float seconds, float easeOut = 0.4f)
        {
            if (I == null || !GameSession.OwnsTime) return;
            I.slowScale = scale;
            I.slowUntil = Time.unscaledTime + seconds;
            I.slowEase = Mathf.Max(0.01f, easeOut);
        }

        void Update()
        {
            float s = 1f;
            float now = Time.unscaledTime;
            if (!GameSession.OwnsTime) { }   // time runs on for everyone
            else if (Paused) s = 0f;
            else if (now < hitStopUntil) s = 0.03f;
            else if (now < slowUntil)
            {
                // ease back to normal speed at the end
                float remain = slowUntil - now;
                s = remain < slowEase ? Mathf.Lerp(1f, slowScale, remain / slowEase) : slowScale;
            }
            Time.timeScale = s;
        }
    }
}
