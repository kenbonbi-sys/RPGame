using UnityEngine;

namespace RPG
{
    /// <summary>
    /// What a hero wants to do this frame. The machine that controls the hero builds it from the
    /// keyboard and mouse; online, a client will send it to the server (Docs/KeHoach-Online.md).
    /// A hero acts on its intent the same way wherever the intent comes from.
    /// </summary>
    public struct PlayerIntent
    {
        /// <summary>Walk direction, length 0..1.</summary>
        public Vector2 move;
        /// <summary>Turn this way while standing still (toward a clicked enemy in reach); zero = keep facing.</summary>
        public Vector2 face;
        /// <summary>Where pressed skills aim.</summary>
        public Vector2 aim;
        /// <summary>Skill keys pressed this frame, bit i = slot i: cast now, or buffered when nearly ready.</summary>
        public int skillPresses;
        /// <summary>Potion keys pressed this frame, bit i = slot i.</summary>
        public int potionPresses;
        /// <summary>Basic attack (slot 0) on a clicked enemy in reach, at <see cref="basicAttackAt"/>.</summary>
        public bool basicAttack;
        public Vector2 basicAttackAt;
        /// <summary>Start talking to this NPC (F next to it, or walked up to a clicked one).</summary>
        public NPC talkTo;

        public bool SkillPressed(int slot) => (skillPresses & (1 << slot)) != 0;

        public bool PotionPressed(int slot) => (potionPresses & (1 << slot)) != 0;

        /// <summary>What carries over to the next frame: walking, facing and aim. Presses happen once.</summary>
        public PlayerIntent Held => new PlayerIntent { move = move, face = face, aim = aim };
    }
}
