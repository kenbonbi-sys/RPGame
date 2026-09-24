using UnityEngine;

namespace RPG
{
    /// <summary>Stun / slow / burn on a character, with visuals.</summary>
    public class StatusEffects : MonoBehaviour
    {
        [Tooltip("Resist stuns entirely (e.g. while shielded).")]
        public bool stunImmune;
        [Tooltip("Multiplier applied to stun durations (bosses resist).")]
        public float stunResist = 1f;

        public float StunRemaining => Mathf.Max(0, stunUntil - Time.time);
        public bool IsStunned => Time.time < stunUntil;
        public bool IsSlowed => Time.time < slowUntil;
        public bool IsBurning => Time.time < burnUntil;
        public float SpeedMultiplier => IsSlowed ? 1f - slowAmount : 1f;

        float stunUntil, slowUntil, burnUntil;
        float slowAmount;
        float burnDps;
        float burnTick;
        Team burnTeam;
        bool burnScaled;
        Health health;
        HitFlash flash;
        GameObject stunFx, burnFx;

        void Awake()
        {
            health = GetComponent<Health>();
            flash = GetComponentInChildren<HitFlash>();
        }

        public void Stun(float seconds)
        {
            if (stunImmune || seconds <= 0) return;
            seconds *= stunResist;
            bool wasStunned = IsStunned;
            stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
            if (!wasStunned && health != null)
            {
                GameEvents.RaiseWorldText("Choáng!", health.HeadPosition + Vector3.up * 0.3f, Palette.Status);
                AudioManager.Play("sfx_stun", 0.7f, 0.05f, transform.position);
            }
        }

        /// <summary>A stun that ignores resistance and immunity (poise breaks, crashing into a boulder).</summary>
        public void ForceStun(float seconds)
        {
            float resist = stunResist;
            bool immune = stunImmune;
            stunResist = 1f;
            stunImmune = false;
            Stun(seconds);
            stunResist = resist;
            stunImmune = immune;
        }

        public void ClearStun() => stunUntil = 0;

        public void Slow(float amount, float seconds)
        {
            slowAmount = Mathf.Max(IsSlowed ? slowAmount : 0f, Mathf.Clamp01(amount));
            slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
        }

        public void Burn(float dps, float seconds, Team team, bool attackScaled = false)
        {
            burnScaled = attackScaled;
            burnDps = Mathf.Max(IsBurning ? burnDps : 0f, dps);
            burnUntil = Mathf.Max(burnUntil, Time.time + seconds);
            burnTeam = team;
        }

        public void Cleanse()
        {
            stunUntil = slowUntil = burnUntil = 0;
        }

        void Update()
        {
            if (health != null && health.IsDead)
            {
                SetFx(ref stunFx, "stun_stars", false);
                SetFx(ref burnFx, "burning", false);
                return;
            }
            // burn damage ticks
            if (IsBurning && health != null)
            {
                burnTick -= Time.deltaTime;
                if (burnTick <= 0)
                {
                    burnTick = 0.5f;
                    var d = DamageInfo.Make(burnDps * 0.5f, burnTeam, null, transform.position, Vector2.up, DamageType.Fire);
                    d.attackScaled = burnScaled;
                    health.TakeDamage(d);
                }
            }
            SetFx(ref stunFx, "stun_stars", IsStunned);
            SetFx(ref burnFx, "burning", IsBurning);
            if (flash != null)
            {
                if (IsSlowed) flash.SetTint(Palette.Ice, 0.35f);
                else if (IsBurning) flash.SetTint(Palette.Fire, 0.18f + 0.1f * Mathf.Sin(Time.time * 18f));
                else flash.SetTint(Color.white, 0f);
            }
        }

        void SetFx(ref GameObject fx, string id, bool on)
        {
            if (on && fx == null)
            {
                var anchor = health != null && health.head != null ? health.head : transform;
                Vector3 pos = id == "stun_stars" ? anchor.position + Vector3.up * 0.2f : transform.position + Vector3.up * 0.4f;
                fx = VFX.Spawn(id, pos, Quaternion.identity, 1f, id == "stun_stars" ? anchor : transform, true);
            }
            else if (!on && fx != null)
            {
                VFX.Release(fx);
                fx = null;
            }
        }

        void OnDisable()
        {
            if (stunFx != null) { VFX.Release(stunFx); stunFx = null; }
            if (burnFx != null) { VFX.Release(burnFx); burnFx = null; }
        }
    }
}
