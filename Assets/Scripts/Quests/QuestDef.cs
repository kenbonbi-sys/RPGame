using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    public enum QuestKind
    {
        Main,
        Side,
        Bounty,
        Hidden
    }

    /// <summary>The nine objective types of plan §09.</summary>
    public enum ObjectiveKind
    {
        /// <summary>Start a conversation with the target NPC.</summary>
        Talk,
        /// <summary>Defeat <c>count</c> enemies with the target id.</summary>
        Kill,
        /// <summary>Carry <c>count</c> of the target item (kept at turn-in).</summary>
        Collect,
        /// <summary>Enter the target zone (zone name) or place id.</summary>
        Reach,
        /// <summary>Use a world object with the target id.</summary>
        Interact,
        /// <summary>Bring the target NPC somewhere safely (reported by the escort script).</summary>
        Escort,
        /// <summary>Hold a place until the defence script reports success.</summary>
        Defend,
        /// <summary>Stay alive for <c>count</c> seconds (reported by the encounter script).</summary>
        Survive,
        /// <summary>Carry <c>count</c> of the target item; it is handed over at turn-in.</summary>
        Deliver
    }

    public enum QuestStatus
    {
        Locked,
        Available,
        Active,
        /// <summary>Every objective is done; waiting to be turned in.</summary>
        Ready,
        Done
    }

    [Serializable]
    public class QuestObjective
    {
        public ObjectiveKind kind = ObjectiveKind.Kill;
        [Tooltip("npc id (Talk, Escort), enemy id (Kill), item id (Collect, Deliver), zone name or place id (Reach, Defend), object id (Interact).")]
        public string target;
        [Min(1)] public int count = 1;
        [Tooltip("Tracker text, e.g. \"Hạ Slime Rêu\". \"(n/count)\" is added when count > 1.")]
        public string text;
        [Tooltip("Kill: kills made before the quest started count too (for unique enemies such as bosses).")]
        public bool countPrevious;
        [Tooltip("Where the minimap star points: an npc id or a place id. Empty = the target.")]
        public string marker;
    }

    [Serializable]
    public class ItemReward
    {
        public ItemDef item;
        [Min(1)] public int count = 1;
    }

    /// <summary>
    /// One quest as data (plan §09): who gives it, what unlocks it, its objectives, rewards,
    /// the flags it sets and the quests that follow. Dialogue starts and turns in quests with
    /// the Yarn commands &lt;&lt;quest_start id&gt;&gt; and &lt;&lt;quest_complete id&gt;&gt;.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Quest")]
    public class QuestDef : ScriptableObject
    {
        public string id;
        public string title;
        public QuestKind kind = QuestKind.Side;
        [TextArea(2, 4)] public string summary;

        [Header("People")]
        [Tooltip("npc id of the quest giver (shows ! over them while the quest is available).")]
        public string giver;
        [Tooltip("npc id to report to. Empty = completes by itself when the objectives are done.")]
        public string turnIn;
        [Tooltip("Tracker text once every objective is done, e.g. \"Báo tin cho Trưởng Làng\".")]
        public string turnInText;

        [Header("Unlock")]
        public int minLevel = 1;
        public List<QuestDef> requires = new List<QuestDef>();
        public List<string> requiredFlags = new List<string>();
        [Tooltip("Becomes active by itself as soon as it unlocks.")]
        public bool autoStart;
        [Tooltip("Tracker text while available but not accepted yet (empty = hidden until accepted).")]
        public string availableText;

        [Header("Objectives")]
        public List<QuestObjective> objectives = new List<QuestObjective>();

        [Header("Rewards")]
        public int xp;
        public int gold;
        public List<ItemReward> items = new List<ItemReward>();
        public List<string> setFlags = new List<string>();
        [Tooltip("Started automatically when this quest is done (if they are unlocked by then).")]
        public List<QuestDef> followUps = new List<QuestDef>();
    }
}
