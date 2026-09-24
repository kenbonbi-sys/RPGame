using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The feel numbers of plan §04 (hit-stop tiers, Lướt Hoàn Hảo), editable in the Inspector
    /// at Assets/Data/Combat.asset. Code reads <see cref="Current"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Combat Config")]
    public class CombatConfig : ScriptableObject
    {
        [Header("Hit-stop (seconds)")]
        [Tooltip("Ordinary hits that have a hit-stop; also the least a kill gets.")]
        public float hitStopLight = 0.035f;
        [Tooltip("Crits and combo finishers.")]
        public float hitStopHeavy = 0.07f;
        [Tooltip("A Thanh Trấn Áp break, and the killing blow on a boss or elite.")]
        public float hitStopBreak = 0.12f;

        [Header("Lướt Hoàn Hảo")]
        [Tooltip("A hit the dash dodges within this many seconds of its start is perfect.")]
        public float perfectWindow = 0.15f;
        [Range(0.05f, 1f)] public float perfectTimeScale = 0.35f;
        [Tooltip("Real seconds of slow motion, the last perfectSlowEase of them easing back.")]
        public float perfectSlowSeconds = 0.3f;
        public float perfectSlowEase = 0.1f;
        public float perfectEnergy = 15f;
        [Tooltip("Damage multiplier of the next skill (1.3 = +30%).")]
        public float perfectDamageMultiplier = 1.3f;
        [Tooltip("Seconds in which the next skill gets the bonus.")]
        public float perfectBonusSeconds = 1.5f;
        [Tooltip("Thanh Trấn Áp the first hit of that skill adds (the counter).")]
        public float perfectCounterPoise = 25f;

        static CombatConfig fallback;

        /// <summary>The config in use: the database's, or built-in defaults when none is assigned.</summary>
        public static CombatConfig Current
        {
            get
            {
                var gm = GameManager.I;
                if (gm != null && gm.db != null && gm.db.combat != null) return gm.db.combat;
                if (fallback == null)
                {
                    fallback = CreateInstance<CombatConfig>();
                    fallback.hideFlags = HideFlags.DontSave;
                }
                return fallback;
            }
        }
    }
}
