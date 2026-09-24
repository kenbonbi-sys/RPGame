using System;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// 8 ability slots (Q W E R A S D Space) with cooldowns, energy costs and levels 1–5.
    /// Hủy đòn (plan §04): a skill waits for the pose of the one before to end, and a key pressed
    /// up to 150 ms early fires the moment it does (the input buffer). A dash may cut a pose short
    /// once the skill's commit window (<see cref="AbilityDef.CommitTime"/>) has passed; a Tuyệt kỹ
    /// commits for its whole pose.
    /// </summary>
    public class PlayerSkills : MonoBehaviour
    {
        public AbilityDef[] slots = new AbilityDef[8];
        [Tooltip("Level 1–5 of each slot's ability (plan §05: +12% power, −4% cooldown per level).")]
        public int[] levels = { 1, 1, 1, 1, 1, 1, 1, 1 };
        public float globalCooldown = 0.08f;
        [Tooltip("A key pressed up to this many seconds before its skill is ready still fires the moment it can (plan §04: 150 ms).")]
        public float inputBuffer = 0.15f;

        /// <summary>slot index when a skill fires.</summary>
        public event Action<int> SkillCast;
        /// <summary>slot index + reason when a skill cannot fire.</summary>
        public event Action<int, string> SkillFailed;
        /// <summary>slot index when a buffered key press finally fires.</summary>
        public event Action<int> BufferedCast;

        /// <summary>The context of the last cast (tests, debug).</summary>
        public AbilityContext LastCast { get; private set; }

        readonly float[] readyAt = new float[8];
        readonly float[] cooldownOf = new float[8];   // cooldown the slot was started with (after stats)
        float gcdUntil;
        float commitUntil;
        PlayerController pc;
        float lastFailMsg;
        int bufferedSlot = -1;
        Vector2 bufferedAim;
        float bufferedUntil;

        void Awake() => pc = GetComponent<PlayerController>();

        /// <summary>Seconds until slot <paramref name="i"/> can fire: its cooldown, the global cooldown or the current pose.</summary>
        public float ReadyIn(int i) => Mathf.Max(Mathf.Max(Remaining(i), gcdUntil - Time.time), PoseWait(i));

        /// <summary>
        /// Seconds slot <paramref name="i"/> waits for the current pose: a dash only for the commit
        /// window of the skill before, any other skill for the whole pose.
        /// </summary>
        public float PoseWait(int i)
        {
            var s = i >= 0 && i < slots.Length ? slots[i] : null;
            if (s == null) return 0f;
            float wait = commitUntil - Time.time;
            if (!s.HasTag(AbilityTags.Movement)) wait = Mathf.Max(wait, pc.ActionRemaining);
            return Mathf.Max(0f, wait);
        }

        public bool HasBuffered => bufferedSlot >= 0;

        /// <summary>
        /// A key press: casts now, or — when only timing is in the way and the skill is ready within
        /// <see cref="inputBuffer"/> — remembers the press and fires it as soon as possible.
        /// Returns true only when the skill fired right away.
        /// </summary>
        public bool Request(int i, Vector2 aim)
        {
            var s = slots[i];
            float wait = ReadyIn(i);
            if (s != null && wait > 0 && wait <= inputBuffer && CanAfford(i))
            {
                bufferedSlot = i;
                bufferedAim = aim;
                // kept until the skill is ready, plus a little in case something else delays it
                bufferedUntil = Time.time + wait + 0.1f;
                return false;
            }
            bufferedSlot = -1;   // a newer press replaces an older buffered one
            return TryCast(i, aim);
        }

        void Update()
        {
            if (bufferedSlot < 0) return;
            var gm = GameManager.I;
            bool canAct = !pc.IsDead && (gm == null || gm.State == GameState.Playing) && (pc.status == null || !pc.status.IsStunned);
            if (!canAct || Time.time > bufferedUntil)
            {
                bufferedSlot = -1;
                return;
            }
            if (ReadyIn(bufferedSlot) > 0) return;
            int slot = bufferedSlot;
            bufferedSlot = -1;
            if (TryCast(slot, bufferedAim)) BufferedCast?.Invoke(slot);
        }

        public float Remaining(int i) => i >= 0 && i < readyAt.Length ? Mathf.Max(0, readyAt[i] - Time.time) : 0;

        public float Fraction(int i)
        {
            var s = slots[i];
            float cd = cooldownOf[i] > 0 ? cooldownOf[i] : s != null ? s.CooldownAt(LevelOf(i)) : 0;
            if (cd <= 0) return 0;
            return Mathf.Clamp01(Remaining(i) / cd);
        }

        public bool CanAfford(int i) => slots[i] != null && pc.energy >= slots[i].energyCost;

        public int LevelOf(int i) => levels != null && i >= 0 && i < levels.Length ? Mathf.Clamp(levels[i], 1, 5) : 1;

        public void ResetCooldowns()
        {
            for (int i = 0; i < readyAt.Length; i++) readyAt[i] = 0;
            gcdUntil = 0;
            commitUntil = 0;
            bufferedSlot = -1;
        }

        public bool TryCast(int i, Vector2 aim)
        {
            var s = slots[i];
            if (s == null) return false;
            if (Time.time < gcdUntil) return false;
            if (PoseWait(i) > 0f) return false;   // still in the pose before (Request buffers presses near its end)
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
            if (s.HasTag(AbilityTags.Movement) && pc.status != null && pc.status.IsRooted)
            {
                Fail(i, "Đang bị Trói!");
                return false;
            }
            Vector2 origin = pc.transform.position;
            Vector2 to = aim - origin;
            if (to.sqrMagnitude < 0.01f) aim = origin + pc.motor.Facing * 0.1f;
            else if (to.magnitude > s.maxRange) aim = origin + to.normalized * s.maxRange;
            int level = LevelOf(i);
            pc.energy -= s.energyCost;
            cooldownOf[i] = s.CooldownAt(level) * (PlayerStats.I != null ? PlayerStats.I.CooldownMultiplier(i, s) : 1f);
            if (i == 0 && pc.status != null) cooldownOf[i] /= Mathf.Max(0.1f, pc.status.AttackSpeedMultiplier);   // Lạnh slows the basic attack
            readyAt[i] = Time.time + cooldownOf[i];
            gcdUntil = Time.time + globalCooldown;
            commitUntil = Time.time + s.CommitTime;
            float bonus = pc.perfectDodge != null ? pc.perfectDodge.TakeBonus(s) : 1f;
            LastCast = AbilityRunner.Cast(s, pc, aim, level, bonus);
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
