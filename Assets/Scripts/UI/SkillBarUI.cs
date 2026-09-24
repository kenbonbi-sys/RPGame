using UnityEngine;

namespace RPG
{
    /// <summary>Hooks the skill slots to the player's skill events.</summary>
    public class SkillBarUI : MonoBehaviour
    {
        public SkillSlotUI[] slots = new SkillSlotUI[8];
        PlayerSkills bound;

        void Update()
        {
            var p = Players.Local;
            if (p == null || p.skills == bound) return;
            if (bound != null)
            {
                bound.SkillCast -= OnCast;
                bound.SkillFailed -= OnFailed;
            }
            bound = p.skills;
            bound.SkillCast += OnCast;
            bound.SkillFailed += OnFailed;
        }

        void OnDestroy()
        {
            if (bound != null)
            {
                bound.SkillCast -= OnCast;
                bound.SkillFailed -= OnFailed;
            }
        }

        void OnCast(int i)
        {
            if (i >= 0 && i < slots.Length && slots[i] != null) slots[i].OnCast();
        }

        void OnFailed(int i, string reason)
        {
            if (i >= 0 && i < slots.Length && slots[i] != null) slots[i].OnFailed();
        }
    }
}
