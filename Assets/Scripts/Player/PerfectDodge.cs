using System;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Lướt Hoàn Hảo (plan §04). A dash opens a short window (0.15 s); an attack that the dash's
    /// invulnerability blocks inside it slows time to 35% for 0.3 s, gives +15 energy and makes
    /// the next skill within 1.5 s hit for +30%, its first hit on a Thanh Trấn Áp adding 25 (the
    /// counter). Bumping into an enemy's body or a damage-over-time tick is not an attack and does
    /// not count. The numbers are in <see cref="CombatConfig"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PerfectDodge : MonoBehaviour
    {
        public const string BuffId = "perfect_dodge";

        /// <summary>Perfect dodges so far.</summary>
        public int Count { get; private set; }
        /// <summary>The next skill gets the damage bonus.</summary>
        public bool BonusReady => Time.time < bonusUntil;
        /// <summary>Raised with the attack that was dodged.</summary>
        public event Action<DamageInfo> Triggered;

        PlayerController pc;
        Health health;
        float windowUntil = -1f;
        bool usedThisDash;
        float bonusUntil = -1f;
        string counterSkill;   // the bonus skill: its first hit on a Thanh Trấn Áp adds the counter poise
        float counterUntil = -1f;
        Sprite icon;

        void Awake()
        {
            pc = GetComponent<PlayerController>();
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (health != null) health.Evaded += OnEvaded;
            GameEvents.Damaged += OnAnyDamaged;
        }

        void OnDisable()
        {
            if (health != null) health.Evaded -= OnEvaded;
            GameEvents.Damaged -= OnAnyDamaged;
        }

        /// <summary>A dash started: an attack dodged in the next moment is perfect. The dash's icon shows on the bonus buff.</summary>
        public void Open(Sprite dashIcon = null)
        {
            windowUntil = Time.time + CombatConfig.Current.perfectWindow;
            usedThisDash = false;
            if (dashIcon != null) icon = dashIcon;
        }

        void OnEvaded(DamageInfo d)
        {
            if (usedThisDash || d.contact || d.dot || Time.time > windowUntil || pc.IsDead) return;
            usedThisDash = true;
            Reward();
            Triggered?.Invoke(d);
        }

        void Reward()
        {
            var c = CombatConfig.Current;
            Count++;
            pc.energy = Mathf.Min(pc.maxEnergy, pc.energy + c.perfectEnergy);
            bonusUntil = Time.time + c.perfectBonusSeconds;
            pc.AddBuff(new BuffSpec { id = BuffId, displayName = "Hoàn Hảo", icon = icon, duration = c.perfectBonusSeconds });

            Vector3 head = health.HeadPosition;
            GameEvents.RaiseWorldText("Hoàn Hảo!", head + Vector3.up * 0.4f, Palette.Gold);
            GameEvents.RaiseWorldText($"+{c.perfectEnergy:0}", head + Vector3.right * 0.5f, Palette.Energy);
            VFX.Spawn("dash_burst", transform.position + Vector3.up * 0.4f, Quaternion.identity, 1.4f);
            if (!pc.IsLocal)
            {
                AudioManager.Play("sfx_crit", 0.8f, 0.02f, transform.position);
                return;
            }
            // slow motion and the flash are the dodger's own (in a shared world TimeFX keeps them on this screen)
            TimeFX.SlowMo(c.perfectTimeScale, c.perfectSlowSeconds, c.perfectSlowEase);
            AudioManager.Play("sfx_crit", 0.8f, 0.02f);
            ScreenFX.Flash(Palette.Gold, 0.15f, 0.25f);
        }

        /// <summary>
        /// A skill is being cast. A non-movement skill inside the bonus window takes the bonus and
        /// ends it: returns its damage multiplier (1 = none). A dash leaves the bonus for the skill after.
        /// </summary>
        public float TakeBonus(AbilityDef ability)
        {
            if (!BonusReady || ability == null || ability.HasTag(AbilityTags.Movement)) return 1f;
            var c = CombatConfig.Current;
            bonusUntil = -1f;
            pc.RemoveBuff(BuffId);
            counterSkill = ability.displayName;
            counterUntil = Time.time + c.perfectBonusSeconds;
            return c.perfectDamageMultiplier;
        }

        void OnAnyDamaged(Health target, DamageInfo d, float amount)
        {
            if (counterSkill == null || d.sourceTeam != Team.Player || d.skillName != counterSkill || d.SourcePlayer != pc) return;
            if (Time.time > counterUntil)
            {
                counterSkill = null;
                return;
            }
            var poise = target.GetComponent<Poise>();
            if (poise == null) return;
            counterSkill = null;
            poise.AddPoise(CombatConfig.Current.perfectCounterPoise);
        }
    }
}
