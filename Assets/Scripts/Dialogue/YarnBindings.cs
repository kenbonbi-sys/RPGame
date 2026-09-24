using UnityEngine;
using Yarn.Unity;

namespace RPG
{
    /// <summary>
    /// What .yarn scripts can ask and do. Yarn Spinner's source generator registers these
    /// automatically, and the Yarn project importer type-checks scripts against them.
    ///
    /// Functions:  quest_status("id") → "locked" | "available" | "active" | "ready" | "done"
    ///             quest_left("id", objective) · quest_progress("id", objective)
    ///             item_count("item_id") · has_flag("flag") · player_level() · is_night()
    /// Commands:   &lt;&lt;quest_start id&gt;&gt; · &lt;&lt;quest_complete id&gt;&gt; · &lt;&lt;set_flag flag&gt;&gt;
    ///             &lt;&lt;give_item item_id count&gt;&gt; · &lt;&lt;victory "title" "subtitle"&gt;&gt;
    /// They read and change the hero in the conversation (<see cref="DialogueDirector.Speaker"/>).
    /// </summary>
    public static class YarnBindings
    {
        static PlayerController Hero
        {
            get
            {
                var d = DialogueDirector.I;
                return d != null && d.Speaker != null ? d.Speaker : Players.Local;
            }
        }

        static QuestSystem Quests => Hero != null ? Hero.quests : null;
        static Inventory Bag => Hero != null ? Hero.inventory : null;

        // ------------------------------------------------------------------ functions
        [YarnFunction("quest_status")]
        public static string QuestStatus(string id)
        {
            var q = Quests;
            return q != null ? q.Status(id).ToString().ToLowerInvariant() : "locked";
        }

        [YarnFunction("quest_left")]
        public static int QuestLeft(string id, int objective) => Quests != null ? Quests.Remaining(id, objective) : 0;

        [YarnFunction("quest_progress")]
        public static int QuestProgress(string id, int objective) => Quests != null ? Quests.Progress(id, objective) : 0;

        [YarnFunction("item_count")]
        public static int ItemCount(string itemId) => Bag != null ? Bag.Count(itemId) : 0;

        [YarnFunction("has_flag")]
        public static bool HasFlag(string flag) => Quests != null && Quests.HasFlag(flag);

        [YarnFunction("player_level")]
        public static int PlayerLevel() => Hero != null && Hero.stats != null ? Hero.stats.level : 1;

        [YarnFunction("is_night")]
        public static bool IsNight() => DayNightCycle.IsNight;

        // ------------------------------------------------------------------ commands
        [YarnCommand("quest_start")]
        public static void QuestStart(string id)
        {
            if (Quests != null) Quests.StartQuest(id);
        }

        [YarnCommand("quest_complete")]
        public static void QuestComplete(string id)
        {
            var q = Quests;
            if (q != null && !q.CompleteQuest(id))
                Debug.LogWarning($"[Yarn] quest_complete {id}: quest is not ready ({q.Status(id)})");
        }

        [YarnCommand("set_flag")]
        public static void SetFlag(string flag)
        {
            if (Quests != null) Quests.SetFlag(flag);
        }

        [YarnCommand("give_item")]
        public static void GiveItem(string itemId, int count = 1)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            var item = db != null ? db.Item(itemId) : null;
            var bag = Bag;
            if (item == null || bag == null)
            {
                Debug.LogWarning("[Yarn] give_item: unknown item " + itemId);
                return;
            }
            bag.Add(item, count);
        }

        [YarnCommand("victory")]
        public static void Victory(string title, string subtitle)
        {
            GameEvents.RaiseBanner(BannerKind.Victory, title, subtitle);
            AudioManager.Play("sfx_levelup", 1f, 0f);
        }
    }
}
