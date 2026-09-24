using System;
using UnityEngine;

namespace RPG
{
    /// <summary>Hit points for anything damageable (player, enemies, boss, boulders).</summary>
    public class Health : MonoBehaviour
    {
        public string displayName = "";
        public int level = 1;
        public Team team = Team.Enemy;
        public float maxHp = 100f;
        public float hp = 100f;
        [Tooltip("Where floating texts and nameplates appear (above the head).")]
        public Transform head;
        [Tooltip("Multiplies incoming damage (shields lower it).")]
        public float damageTakenMultiplier = 1f;
        [Tooltip("Reduces incoming damage: armor / (armor + 50 + 5 × attacker level).")]
        public float armor;
        [Tooltip("0..0.75 — reduces every non-physical damage type.")]
        public float elementalResist;
        [Tooltip("Per damage type, on top of elementalResist: −0.5 (weakness, +50% damage) … 0.75.")]
        public Resistances resistances;
        public bool invulnerable;
        public bool showDamageNumbers = true;

        public event Action<DamageInfo, float> Damaged;
        public event Action<DamageInfo> Died;
        public event Action<float> Healed;
        /// <summary>A hostile hit that invulnerability blocked (a dash's i-frames); Lướt Hoàn Hảo listens.</summary>
        public event Action<DamageInfo> Evaded;

        /// <summary>Source of the random damage spread (0..1). Tests pin it to 0.5 for exact numbers.</summary>
        public static Func<float> SpreadRoll = () => UnityEngine.Random.value;

        public bool IsDead { get; private set; }
        public float Fraction => maxHp > 0 ? Mathf.Clamp01(hp / maxHp) : 0f;
        public float LastDamageTime { get; private set; } = -99f;
        public Vector3 HeadPosition => head != null ? head.position : transform.position + Vector3.up * 1.2f;

        StatusEffects status;

        void Awake()
        {
            status = GetComponent<StatusEffects>();
        }

        public void ResetHealth(float max = -1)
        {
            if (max > 0) maxHp = max;
            hp = maxHp;
            IsDead = false;
        }

        public bool CanBeDamagedBy(Team attacker)
        {
            if (attacker == team) return false;
            if (team == Team.Neutral) return attacker == Team.Player;
            return true;
        }

        /// <summary>Total resistance to a damage type: the type's own plus, for elements, the all-element resist; clamped.</summary>
        public float Resistance(DamageType type)
        {
            var c = ProgressionConfig.Current;
            float r = resistances[type] + (type == DamageType.Physical ? 0f : elementalResist);
            return Mathf.Clamp(r, c.resistMin, c.resistMax);
        }

        /// <summary>Applies damage (armor, resistance and the random spread of plan §04). Returns the amount actually dealt.</summary>
        public float TakeDamage(DamageInfo d)
        {
            if (IsDead || !CanBeDamagedBy(d.sourceTeam)) return 0f;
            if (invulnerable)
            {
                Evaded?.Invoke(d);
                return 0f;
            }
            float raw = d.amount;
            if (!d.pure)
            {
                raw *= damageTakenMultiplier;
                // older damage sources carry flat numbers: scale them by the hero's Attack here, once
                if (d.sourceTeam == Team.Player && !d.attackScaled && PlayerStats.I != null) raw *= PlayerStats.I.DamageScale(d.type);
                raw = ProgressionConfig.Current.Mitigate(raw, armor, armor > 0 ? AttackerLevel(d) : 1, Resistance(d.type), SpreadRoll());
            }
            float amount = Mathf.Max(1f, Mathf.Round(raw));
            hp = Mathf.Max(0f, hp - amount);
            LastDamageTime = Time.time;

            if (status != null)
            {
                if (d.stun > 0) status.Stun(d.stun);
                if (d.slow > 0) status.Slow(d.slow, d.slowDuration > 0 ? d.slowDuration : 2f);
                if (d.burnDps > 0) status.Burn(d.burnDps, d.burnDuration > 0 ? d.burnDuration : 3f, d.sourceTeam, d.attackScaled);
            }

            Damaged?.Invoke(d, amount);
            GameEvents.RaiseDamaged(this, d, amount);

            if (hp <= 0f)
            {
                IsDead = true;
                Died?.Invoke(d);
                GameEvents.RaiseDied(this);
            }
            return amount;
        }

        int AttackerLevel(DamageInfo d)
        {
            if (d.sourceLevel > 0) return d.sourceLevel;
            var src = d.source != null ? d.source.GetComponentInParent<Health>() : null;
            return src != null ? src.level : level;
        }

        public void Heal(float amount, bool showText = true)
        {
            if (IsDead || amount <= 0) return;
            float before = hp;
            hp = Mathf.Min(maxHp, hp + amount);
            float healed = hp - before;
            if (healed <= 0) return;
            Healed?.Invoke(healed);
            if (showText) GameEvents.RaiseHealed(this, healed);
        }

        public void Kill()
        {
            if (IsDead) return;
            var d = DamageInfo.Make(hp + 1, team == Team.Player ? Team.Enemy : Team.Player, null, transform.position, Vector2.down);
            d.pure = true;   // armor or the spread must not leave it alive
            invulnerable = false;
            TakeDamage(d);
        }
    }
}
