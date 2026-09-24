using System;
using UnityEngine;

namespace RPG
{
    /// <summary>Who died, for quests, XP and the bestiary.</summary>
    public struct KillInfo
    {
        public string id;
        public string name;
        public int level;
        public EnemyRank rank;
        public Vector3 position;
    }

    /// <summary>Global game events. Systems publish here, UI and quests listen.</summary>
    public static class GameEvents
    {
        public static event Action<Health, DamageInfo, float> Damaged;   // target, info, applied amount
        public static event Action<Health, float> Healed;
        public static event Action<Health> Died;
        public static event Action<string, Color> Log;
        public static event Action<KillInfo> EnemyKilled;
        public static event Action<int> LevelUp;                         // new level
        public static event Action<ItemDef, int> ItemPicked;
        public static event Action QuestChanged;
        public static event Action<string> QuestCompleted;               // quest title
        public static event Action<string> ZoneEntered;                  // zone display name
        public static event Action<string, Vector3, Color> WorldText;    // free floating text

        public static void RaiseDamaged(Health h, DamageInfo d, float applied) => Damaged?.Invoke(h, d, applied);
        public static void RaiseHealed(Health h, float amount) => Healed?.Invoke(h, amount);
        public static void RaiseDied(Health h) => Died?.Invoke(h);
        public static void RaiseLog(string msg, Color c) => Log?.Invoke(msg, c);
        public static void RaiseLog(string msg) => Log?.Invoke(msg, Palette.LogInfo);
        public static void RaiseEnemyKilled(KillInfo k) => EnemyKilled?.Invoke(k);
        public static void RaiseLevelUp(int level) => LevelUp?.Invoke(level);
        public static void RaiseItemPicked(ItemDef item, int n) => ItemPicked?.Invoke(item, n);
        public static void RaiseQuestChanged() => QuestChanged?.Invoke();
        public static void RaiseQuestCompleted(string title) => QuestCompleted?.Invoke(title);
        public static void RaiseZoneEntered(string zone) => ZoneEntered?.Invoke(zone);
        public static void RaiseWorldText(string text, Vector3 pos, Color c) => WorldText?.Invoke(text, pos, c);

        /// <summary>Clears all listeners (called when the game scene boots, keeps domain-reload-off safe).</summary>
        public static void Reset()
        {
            Damaged = null; Healed = null; Died = null; Log = null; EnemyKilled = null; LevelUp = null;
            ItemPicked = null; QuestChanged = null; QuestCompleted = null; ZoneEntered = null; WorldText = null;
        }
    }
}
