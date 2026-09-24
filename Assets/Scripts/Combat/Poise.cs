using System;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Thanh Trấn Áp (plan §04) for bosses and elites. The hero's hits fill it (each skill has a
    /// poise value, Strength adds more); when full the target is stunned for a few seconds and
    /// takes extra damage. Without hits for a moment it drains, and every break raises the threshold.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Poise : MonoBehaviour
    {
        public float maxPoise = 300f;
        [Tooltip("Seconds without a hit before the bar starts to drain.")]
        public float decayDelay = 2.5f;
        public float decayPerSecond = 40f;
        public float breakStun = 3f;
        [Tooltip("Extra damage taken while broken (0.5 = +50%).")]
        public float brokenDamageBonus = 0.5f;
        [Tooltip("Each break raises the threshold by this share of maxPoise.")]
        public float thresholdGrowth = 0.25f;

        public float Current { get; private set; }
        public float Threshold { get; private set; }
        public int Breaks { get; private set; }
        public bool IsBroken => Time.time < brokenUntil;
        /// <summary>0..1 for the HUD (full while broken).</summary>
        public float Fraction => IsBroken ? 1f : Threshold > 0 ? Mathf.Clamp01(Current / Threshold) : 0f;
        public float BrokenRemaining => Mathf.Max(0f, brokenUntil - Time.time);

        /// <summary>Raised when the bar breaks (the owner interrupts its attack).</summary>
        public event Action Broken;

        Health health;
        StatusEffects status;
        float lastHit = -99f;
        float brokenUntil;
        bool bonusApplied;

        void Awake()
        {
            health = GetComponent<Health>();
            status = GetComponent<StatusEffects>();
            Threshold = maxPoise;
            health.Damaged += OnDamaged;
        }

        /// <summary>Back to an empty bar and the base threshold (fight reset).</summary>
        public void ResetPoise()
        {
            EndBreak();
            Current = 0f;
            Breaks = 0;
            Threshold = maxPoise;
        }

        void OnDamaged(DamageInfo d, float amount)
        {
            if (d.sourceTeam != Team.Player || d.poise <= 0f) return;
            AddPoise(d.poise * (PlayerStats.I != null ? PlayerStats.I.PoiseMultiplier : 1f));
        }

        /// <summary>Fills the bar (the hero's hits, the counter after Lướt Hoàn Hảo); breaks it when full.</summary>
        public void AddPoise(float amount)
        {
            if (amount <= 0f || IsBroken || health.IsDead) return;
            Current += amount;
            lastHit = Time.time;
            if (Current >= Threshold) Break();
        }

        void Break()
        {
            Breaks++;
            Current = 0f;
            Threshold = maxPoise * (1f + thresholdGrowth * Breaks);
            brokenUntil = Time.time + breakStun;
            if (status != null) status.ForceStun(breakStun);
            if (!bonusApplied)
            {
                health.damageTakenMultiplier *= 1f + brokenDamageBonus;
                bonusApplied = true;
            }

            Vector3 head = health.HeadPosition;
            GameEvents.RaiseWorldText("Vỡ Trấn Áp!", head + Vector3.up * 0.6f, Palette.Gold);
            GameEvents.RaiseLog($"{health.displayName} bị vỡ Trấn Áp! Choáng {breakStun:0.#} giây, nhận thêm {brokenDamageBonus * 100f:0}% sát thương.", Palette.Status);
            VFX.Spawn("boulder_break", transform.position + Vector3.up * 0.8f, Quaternion.identity, 1.3f);
            AudioManager.Play("sfx_boulder_break", 1f, 0.03f, transform.position);
            AudioManager.Play("sfx_crit", 0.8f, 0.02f);
            TimeFX.HitStop(CombatConfig.Current.hitStopBreak);
            CameraRig.Shake(0.5f);
            ScreenFX.Impact(0.6f, 0.4f);
            Broken?.Invoke();
        }

        void Update()
        {
            if (bonusApplied && !IsBroken) EndBreak();
            if (!IsBroken && Current > 0f && Time.time - lastHit > decayDelay)
                Current = Mathf.Max(0f, Current - decayPerSecond * Time.deltaTime);
        }

        void EndBreak()
        {
            brokenUntil = 0f;
            if (!bonusApplied) return;
            health.damageTakenMultiplier /= 1f + brokenDamageBonus;
            bonusApplied = false;
        }
    }
}
