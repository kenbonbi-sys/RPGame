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
            // menus and conversations on this screen hold only the hero this screen controls
            bool screenFree = !pc.IsLocal || gm == null || gm.State == GameState.Playing;
            bool canAct = !pc.IsDead && screenFree && (pc.status == null || !pc.status.IsStunned);
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

        /// <summary>
        /// Puts a class's skills on the bar: Q is the weapon's basic attack (a spellcaster's focus
        /// leaves Q to the class's cantrip), W E R A S D the class's own (or the learned spells the
        /// look puts there, <see cref="Spellbook"/>), Space Lướt for everyone. Slots whose skill is
        /// missing keep what they had.
        /// </summary>
        public void ApplyKit(ClassDef cls, WeaponKind weapon, HeroLook look = null)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (cls == null || db == null) return;
            // a skill's cooldown goes with the skill, not the slot: moving spells around the bar
            // (Sách Chiêu) or forging a weapon must not make a Tuyệt kỹ ready again
            var was = new AbilityDef[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                was[i] = slots[i];
                if (slots[i] != null && readyAt[i] > Time.time) heldCooldown[slots[i]] = (readyAt[i], cooldownOf[i]);
            }
            for (int i = 0; i < 7; i++)
            {
                string id = cls.kit != null && i < cls.kit.Length ? cls.kit[i] : null;
                if (i == 0 && weapon != null && !weapon.focus && !string.IsNullOrEmpty(weapon.basic)) id = weapon.basic;
                var a = string.IsNullOrEmpty(id) ? null : db.Ability(id);
                if (a != null) slots[i] = a;
            }
            for (int i = Spellbook.FirstSlot; i <= Spellbook.UltimateSlot; i++)
            {
                var spell = Spellbook.BarAbility(look, i);
                if (spell != null) slots[i] = spell;
            }
            var dash = db.Ability("dash");
            if (dash != null) slots[7] = dash;
            gcdUntil = 0;
            commitUntil = 0;
            bufferedSlot = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == was[i]) continue;
                readyAt[i] = 0f;
                if (slots[i] != null && heldCooldown.TryGetValue(slots[i], out var held) && held.ready > Time.time)
                {
                    readyAt[i] = held.ready;
                    cooldownOf[i] = held.length;
                }
            }
        }

        /// <summary>Cooldowns of skills that left the bar, so one put back is not ready early.</summary>
        readonly System.Collections.Generic.Dictionary<AbilityDef, (float ready, float length)> heldCooldown =
            new System.Collections.Generic.Dictionary<AbilityDef, (float ready, float length)>();

        public void ResetCooldowns()
        {
            for (int i = 0; i < readyAt.Length; i++) readyAt[i] = 0;
            heldCooldown.Clear();
            gcdUntil = 0;
            commitUntil = 0;
            bufferedSlot = -1;
        }

        /// <summary>
        /// Casts slot <paramref name="i"/> toward <paramref name="aim"/> when it is ready. Where the
        /// world's rules run the skill happens for real; a player's machine online shows it at once
        /// (pose, effects, its dash) and asks the server, which deals the damage and shows it to
        /// the others (and says so when it refuses: <see cref="Refused"/>).
        /// </summary>
        public bool TryCast(int i, Vector2 aim)
        {
            if (!CanCast(i, 0f, true, out _)) return false;
            var s = slots[i];
            Vector2 origin = pc.transform.position;
            aim = ClampAim(s, origin, aim);
            int level = LevelOf(i);
            Commit(i, s, level);
            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            if (GameSession.IsAuthority)
            {
                float bonus = pc.perfectDodge != null ? pc.perfectDodge.TakeBonus(s) : 1f;
                LastCast = AbilityRunner.Cast(s, pc, aim, level, bonus, CastMode.Live, seed);
                if (GameSession.Serving) NetWorld.CastShown(pc, i, aim, origin, level, seed);
            }
            else
            {
                pc.HoldEnergy(0.6f);
                LastCast = AbilityRunner.Cast(s, pc, aim, level, 1f, CastMode.Predicted, seed);
                OnlineSession.Ask(new CastRequest { slot = (byte)i, aim = aim, origin = origin });
            }
            SkillCast?.Invoke(i);
            return true;
        }

        /// <summary>
        /// Whether slot <paramref name="i"/> may fire now: global cooldown, the pose before, its own
        /// cooldown, energy, Trói. <paramref name="grace"/> forgives timing (the server hears a
        /// press a moment after the player's machine allowed it).
        /// </summary>
        bool CanCast(int i, float grace, bool feedback, out string reason)
        {
            reason = null;
            var s = i >= 0 && i < slots.Length ? slots[i] : null;
            if (s == null) return false;
            if (Time.time < gcdUntil - grace) return false;
            if (PoseWait(i) > grace) return false;   // still in the pose before (Request buffers presses near its end)
            if (Remaining(i) > grace)
            {
                if (feedback) Fail(i, null);
                return false;
            }
            if (pc.energy < s.energyCost)
            {
                reason = "Không đủ năng lượng!";
                if (feedback) Fail(i, reason);
                return false;
            }
            if (s.HasTag(AbilityTags.Movement) && pc.status != null && pc.status.IsRooted)
            {
                reason = "Đang bị Trói!";
                if (feedback) Fail(i, reason);
                return false;
            }
            return true;
        }

        Vector2 ClampAim(AbilityDef s, Vector2 origin, Vector2 aim)
        {
            Vector2 to = aim - origin;
            if (to.sqrMagnitude < 0.01f) return origin + pc.motor.Facing * 0.1f;
            if (to.magnitude > s.maxRange) return origin + to.normalized * s.maxRange;
            return aim;
        }

        /// <summary>Spends the energy and starts the cooldowns of a cast.</summary>
        void Commit(int i, AbilityDef s, int level)
        {
            pc.energy -= s.energyCost;
            cooldownOf[i] = s.CooldownAt(level) * (pc.stats != null ? pc.stats.CooldownMultiplier(i, s) : 1f);
            if (i == 0 && pc.status != null) cooldownOf[i] /= Mathf.Max(0.1f, pc.status.AttackSpeedMultiplier);   // Lạnh slows the basic attack
            readyAt[i] = Time.time + cooldownOf[i];
            gcdUntil = Time.time + globalCooldown;
            commitUntil = Time.time + s.CommitTime;
        }

        // ------------------------------------------------------------------ online
        /// <summary>How much earlier than the server a player's machine may think a skill is ready.</summary>
        const float ServerGrace = 0.15f;

        /// <summary>
        /// Server: a player used slot <paramref name="i"/> from <paramref name="origin"/> (where their
        /// hero stood on their screen). Returns null when the skill happened, else why not.
        /// </summary>
        public string ServerCast(int i, Vector2 aim, Vector2 origin)
        {
            if (pc.IsDead) return "";
            if (pc.status != null && pc.status.IsStunned) return "Đang bị choáng!";
            if (!CanCast(i, ServerGrace, false, out string reason)) return reason ?? "";
            // the player's own position, unless it is far from what the server last heard
            if (((Vector2)pc.transform.position - origin).sqrMagnitude > 3f * 3f) origin = pc.transform.position;
            var s = slots[i];
            aim = ClampAim(s, origin, aim);
            int level = LevelOf(i);
            Commit(i, s, level);
            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            float bonus = pc.perfectDodge != null ? pc.perfectDodge.TakeBonus(s) : 1f;
            LastCast = AbilityRunner.Cast(s, pc, aim, level, bonus, GameSession.HasScreen ? CastMode.Live : CastMode.Rules, seed, origin);
            NetWorld.CastShown(pc, i, aim, origin, level, seed);
            SkillCast?.Invoke(i);
            return null;
        }

        /// <summary>A player's machine: the server refused a skill this screen already showed. Its cooldown is given back.</summary>
        public void Refused(int i, string reason)
        {
            if (i < 0 || i >= readyAt.Length) return;
            readyAt[i] = 0f;
            gcdUntil = 0f;
            commitUntil = 0f;
            pc.HoldEnergy(0f);
            if (!string.IsNullOrEmpty(reason)) Fail(i, reason);
        }

        /// <summary>Another player's hero used a skill (the server says so): show it on this screen.</summary>
        public void ShowCast(int i, Vector2 aim, Vector2 origin, int level, int seed)
        {
            var s = i >= 0 && i < slots.Length ? slots[i] : null;
            if (s == null) return;
            LastCast = AbilityRunner.Cast(s, pc, aim, level, 1f, CastMode.Shown, seed, origin);
        }

        void Fail(int i, string reason)
        {
            SkillFailed?.Invoke(i, reason);
            if (reason != null && pc.IsLocal && Time.time - lastFailMsg > 0.8f)
            {
                lastFailMsg = Time.time;
                GameEvents.RaiseWorldText(reason, pc.health.HeadPosition + Vector3.up * 0.4f, Palette.Energy);
                AudioManager.Play("sfx_denied", 0.6f);
            }
        }
    }
}
