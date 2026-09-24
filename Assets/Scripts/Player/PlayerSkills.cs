using System;
using UnityEngine;

namespace RPG
{
    /// <summary>8 skill slots (Q W E R A S D Space) with cooldowns and energy costs.</summary>
    public class PlayerSkills : MonoBehaviour
    {
        public SkillDef[] slots = new SkillDef[8];
        public float globalCooldown = 0.08f;

        /// <summary>slot index when a skill fires.</summary>
        public event Action<int> SkillCast;
        /// <summary>slot index + reason when a skill cannot fire.</summary>
        public event Action<int, string> SkillFailed;

        readonly float[] readyAt = new float[8];
        readonly float[] cooldownOf = new float[8];   // cooldown the slot was started with (after stats)
        float gcdUntil;
        PlayerController pc;
        float lastFailMsg;

        void Awake() => pc = GetComponent<PlayerController>();

        public float Remaining(int i) => i >= 0 && i < readyAt.Length ? Mathf.Max(0, readyAt[i] - Time.time) : 0;

        public float Fraction(int i)
        {
            var s = slots[i];
            float cd = cooldownOf[i] > 0 ? cooldownOf[i] : s != null ? s.cooldown : 0;
            if (cd <= 0) return 0;
            return Mathf.Clamp01(Remaining(i) / cd);
        }

        public bool CanAfford(int i) => slots[i] != null && pc.energy >= slots[i].energyCost;

        public void ResetCooldowns()
        {
            for (int i = 0; i < readyAt.Length; i++) readyAt[i] = 0;
        }

        public bool TryCast(int i, Vector2 aim)
        {
            var s = slots[i];
            if (s == null) return false;
            if (Time.time < gcdUntil) return false;
            if (Remaining(i) > 0)
            {
                Fail(i, null);
                return false;
            }
            if (pc.energy < s.energyCost)
            {
                Fail(i, "Không đủ năng lượng!");
                return false;
            }
            Vector2 origin = pc.transform.position;
            Vector2 to = aim - origin;
            if (to.sqrMagnitude < 0.01f) to = pc.motor.Facing;
            if (to.magnitude > s.maxRange) aim = origin + to.normalized * s.maxRange;
            var ctx = new SkillContext
            {
                caster = pc,
                origin = origin,
                aim = aim,
                dir = to.normalized,
                team = Team.Player
            };
            pc.energy -= s.energyCost;
            cooldownOf[i] = s.cooldown * (PlayerStats.I != null ? PlayerStats.I.CooldownMultiplier(i, s) : 1f);
            readyAt[i] = Time.time + cooldownOf[i];
            gcdUntil = Time.time + globalCooldown;
            pc.BeginAction(s.animBase, ctx.dir, s.lockTime, s.moveWhileCasting);
            if (!string.IsNullOrEmpty(s.castSfx)) AudioManager.Play(s.castSfx, 0.9f, 0.06f);
            s.Execute(ctx);
            SkillCast?.Invoke(i);
            return true;
        }

        void Fail(int i, string reason)
        {
            SkillFailed?.Invoke(i, reason);
            if (reason != null && Time.time - lastFailMsg > 0.8f)
            {
                lastFailMsg = Time.time;
                GameEvents.RaiseWorldText(reason, pc.health.HeadPosition + Vector3.up * 0.4f, Palette.Energy);
                AudioManager.Play("sfx_denied", 0.6f);
            }
        }
    }
}
