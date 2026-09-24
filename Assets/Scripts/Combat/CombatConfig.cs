using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The feel numbers of plan §04 (hit-stop tiers, Lướt Hoàn Hảo, statuses and crowd control),
    /// editable in the Inspector at Assets/Data/Combat.asset. Code reads <see cref="Current"/>.
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

        [Header("Statuses")]
        [Tooltip("Seconds between two damage ticks of Bỏng and Độc.")]
        public float tickInterval = 0.5f;
        [Tooltip("Bỏng: damage per second of a stack, as a share of the hit that set it.")]
        public float burnShare = 0.3f;
        public int burnMaxStacks = 3;
        public float burnSeconds = 3f;
        [Tooltip("Lạnh: move and attack speed lost per stack.")]
        public float chillSlowPerStack = 0.12f;
        [Tooltip("The stack that freezes.")]
        public int chillMaxStacks = 4;
        public float chillSeconds = 4f;
        public float freezeSeconds = 1.5f;
        [Tooltip("Đóng Băng on a boss or elite: shorter, and it adds Thanh Trấn Áp.")]
        public float heavyFreezeSeconds = 0.6f;
        public float heavyFreezePoise = 60f;
        [Tooltip("Tích Điện: the stack that discharges.")]
        public int chargeMaxStacks = 3;
        public float chargeSeconds = 5f;
        [Tooltip("Discharge damage as a share of the hit that set the last stack.")]
        public float dischargeShare = 0.8f;
        public int dischargeTargets = 3;
        public float dischargeRadius = 4f;
        [Tooltip("Độc: share of max HP per second per stack.")]
        public float poisonMaxHpPerSecond = 0.015f;
        public int poisonMaxStacks = 5;
        public float poisonSeconds = 6f;
        [Tooltip("Làm Chậm seconds when a hit gives none.")]
        public float slowSeconds = 2f;
        [Tooltip("Nguyền: extra damage taken and damage dealt lost.")]
        public float curseDamageTaken = 0.15f;
        public float curseDamageDealt = 0.2f;
        [Tooltip("Phán Xét: extra damage taken.")]
        public float judgmentDamageTaken = 0.2f;

        [Header("Crowd control")]
        [Tooltip("Choáng, Đóng Băng or Trói landing again within this many seconds lasts shorter…")]
        public float crowdControlRepeatWindow = 6f;
        [Tooltip("…by this factor each time (0.6 = 40% shorter).")]
        public float crowdControlRepeatFactor = 0.6f;
        [Tooltip("A boss or elite ignores crowd control for this long after a stun or freeze ends.")]
        public float heavyCrowdControlImmunity = 4f;
        [Tooltip("Đẩy Lùi faster than this (m/s) into a wall stuns…")]
        public float wallSlamSpeed = 5f;
        [Tooltip("…for this long.")]
        public float wallSlamStun = 0.5f;

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
