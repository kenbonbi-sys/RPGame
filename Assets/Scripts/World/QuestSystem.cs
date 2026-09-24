using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The prototype's quest line (kept in code so it is easy to read and change):
    ///   Main: talk to the chief → clear the forest → defeat Gấu Ma Rừng Già → report back.
    ///   Side: Bé Mai wants 3 mushroom caps.
    /// </summary>
    public class QuestSystem : MonoBehaviour, ISaveable
    {
        public static QuestSystem I { get; private set; }

        public enum Marker { None, Exclaim, Question }

        public enum Main { TalkToChief, ClearForest, SlayBoss, ReportBack, Done }
        public enum Side { NotStarted, Collecting, Done }

        public Main main = Main.TalkToChief;
        public Side side = Side.NotStarted;
        public int slimeGoal = 4, shroomGoal = 2, capGoal = 3;
        public int slimes, shrooms;

        [Header("XP rewards")]
        public int xpTalkToChief = 20;
        public int xpClearForest = 120;
        public int xpSlayBoss = 400;
        public int xpMushrooms = 80;

        /// <summary>Which tracked quest is shown first in the HUD (Tab cycles).</summary>
        public int focus;

        void Awake() => I = this;

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnKilled;
            GameEvents.ItemPicked += OnItem;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnKilled;
            GameEvents.ItemPicked -= OnItem;
        }

        // ------------------------------------------------------------ tracker
        public struct Entry
        {
            public string title;
            public string objective;
            public bool main;
        }

        public List<Entry> Tracked()
        {
            var list = new List<Entry>();
            switch (main)
            {
                case Main.TalkToChief: list.Add(new Entry { title = "Lời Nhờ Của Trưởng Làng", objective = "Nói chuyện với Trưởng Làng", main = true }); break;
                case Main.ClearForest:
                    list.Add(new Entry
                    {
                        title = "Dọn Dẹp Rừng Thì Thầm",
                        objective = $"Hạ Slime Rêu ({Mathf.Min(slimes, slimeGoal)}/{slimeGoal}) · Nấm Độc ({Mathf.Min(shrooms, shroomGoal)}/{shroomGoal})",
                        main = true
                    });
                    break;
                case Main.SlayBoss: list.Add(new Entry { title = "Gấu Ma Rừng Già", objective = "Đánh bại Gấu Ma Rừng Già", main = true }); break;
                case Main.ReportBack: list.Add(new Entry { title = "Trở Về Làng", objective = "Báo tin cho Trưởng Làng", main = true }); break;
            }
            if (side == Side.Collecting)
            {
                int caps = Inventory.I != null ? Inventory.I.Count("shroom_cap") : 0;
                list.Add(new Entry { title = "Nấm Cho Bé Mai", objective = caps >= capGoal ? "Mang nấm về cho Bé Mai" : $"Nhặt Mũ Nấm Đỏ ({caps}/{capGoal})" });
            }
            else if (side == Side.NotStarted && main != Main.TalkToChief)
            {
                list.Add(new Entry { title = "Cô Bé Bên Giếng", objective = "Nói chuyện với Bé Mai" });
            }
            if (list.Count > 0) focus = Mathf.Clamp(focus, 0, list.Count - 1);
            return list;
        }

        public void CycleFocus()
        {
            int n = Tracked().Count;
            if (n > 1)
            {
                focus = (focus + 1) % n;
                GameEvents.RaiseQuestChanged();
                AudioManager.Play("sfx_ui_click", 0.6f);
            }
        }

        public Marker MarkerFor(string npc)
        {
            if (npc == "chief")
            {
                if (main == Main.TalkToChief) return Marker.Exclaim;
                if (main == Main.ReportBack) return Marker.Question;
            }
            if (npc == "girl")
            {
                if (side == Side.NotStarted && main != Main.TalkToChief) return Marker.Exclaim;
                if (side == Side.Collecting && Inventory.I != null && Inventory.I.Count("shroom_cap") >= capGoal) return Marker.Question;
            }
            return Marker.None;
        }

        /// <summary>Where the current focused objective is (for the minimap star).</summary>
        public Vector3? ObjectivePosition()
        {
            var gm = GameManager.I;
            if (gm == null) return null;
            var list = Tracked();
            if (list.Count == 0) return null;
            var e = list[Mathf.Clamp(focus, 0, list.Count - 1)];
            if (!e.main) return gm.girlSpot != null ? gm.girlSpot.position : (Vector3?)null;
            switch (main)
            {
                case Main.TalkToChief:
                case Main.ReportBack: return gm.chiefSpot != null ? gm.chiefSpot.position : (Vector3?)null;
                case Main.ClearForest: return gm.forestSpot != null ? gm.forestSpot.position : (Vector3?)null;
                case Main.SlayBoss: return gm.bossSpot != null ? gm.bossSpot.position : (Vector3?)null;
            }
            return null;
        }

        // ------------------------------------------------------------ dialogue
        public List<DialogueLine> GetDialogue(string npc)
        {
            const string chief = "Trưởng Làng";
            const string mai = "Bé Mai";
            var L = new List<DialogueLine>();
            if (npc == "chief")
            {
                switch (main)
                {
                    case Main.TalkToChief:
                        L.Add(new DialogueLine(chief, "A, cháu đến rồi! Làng Lá Xanh đang gặp chuyện lớn..."));
                        L.Add(new DialogueLine(chief, "Từ khi trăng máu mọc, lũ Slime Rêu và Nấm Độc tràn ra khắp Rừng Thì Thầm."));
                        L.Add(new DialogueLine(chief, "Cháu hãy dọn bớt chúng đi. Nhớ dùng <color=#ffe07a>Q W E R</color> để ra chiêu, <color=#ffe07a>Space</color> để lướt né đòn."));
                        L.Add(new DialogueLine(chief, "Cầm lấy mấy bình thuốc này. Bấm <color=#ffe07a>1 2 3</color> khi cần nhé!"));
                        break;
                    case Main.ClearForest:
                        L.Add(new DialogueLine(chief, $"Rừng Thì Thầm ở phía đông. Còn {Mathf.Max(0, slimeGoal - slimes)} Slime Rêu và {Mathf.Max(0, shroomGoal - shrooms)} Nấm Độc nữa."));
                        break;
                    case Main.SlayBoss:
                        L.Add(new DialogueLine(chief, "Gấu Ma Rừng Già ngự ở Rừng Già Cổ Thụ, phía đông bắc."));
                        L.Add(new DialogueLine(chief, "Nó biết <color=#ff9a7a>Dậm Đất</color> làm choáng, <color=#ff9a7a>Ném Đá Lớn</color> và <color=#ff9a7a>Chụp Quăng</color>. Nhìn vòng cảnh báo đỏ mà né!"));
                        L.Add(new DialogueLine(chief, "Mẹo nhỏ: đứng sau Tảng Đá Lớn khi nó vồ tới... con gấu sẽ tự đâm đầu vào đá đấy."));
                        break;
                    case Main.ReportBack:
                        L.Add(new DialogueLine(chief, "Cháu... cháu đã hạ được Gấu Ma Rừng Già thật sao?!"));
                        L.Add(new DialogueLine(chief, "Cả làng nợ cháu một ân tình. Đây là phần thưởng xứng đáng!"));
                        break;
                    case Main.Done:
                        L.Add(new DialogueLine(chief, "Rừng đã yên bình trở lại. Cảm ơn cháu, người hùng của Làng Lá Xanh!"));
                        break;
                }
            }
            else if (npc == "girl")
            {
                int caps = Inventory.I != null ? Inventory.I.Count("shroom_cap") : 0;
                switch (side)
                {
                    case Side.NotStarted:
                        if (main == Main.TalkToChief)
                        {
                            L.Add(new DialogueLine(mai, "Anh chị ơi, ông Trưởng Làng đang tìm anh chị đó! Ông đứng cạnh đống lửa."));
                        }
                        else
                        {
                            L.Add(new DialogueLine(mai, "Mẹ em ốm rồi... Em cần 3 cái Mũ Nấm Đỏ để nấu thuốc."));
                            L.Add(new DialogueLine(mai, "Lũ Nấm Độc trong rừng hay rơi ra lắm. Anh chị giúp em nhé?"));
                        }
                        break;
                    case Side.Collecting:
                        if (caps >= capGoal)
                        {
                            L.Add(new DialogueLine(mai, "Oa, đủ nấm rồi! Em cảm ơn nhiều lắm!"));
                            L.Add(new DialogueLine(mai, "Đây là thuốc xanh bà em làm, uống vào khỏe re luôn!"));
                        }
                        else L.Add(new DialogueLine(mai, $"Em cần {capGoal} Mũ Nấm Đỏ... mới có {caps} thôi."));
                        break;
                    case Side.Done:
                        L.Add(new DialogueLine(mai, "Mẹ em đỡ nhiều rồi! Anh chị là nhất!"));
                        break;
                }
            }
            return L;
        }

        public void OnTalked(string npc)
        {
            var db = GameManager.I.db;
            if (npc == "chief")
            {
                if (main == Main.TalkToChief)
                {
                    Complete("Lời Nhờ Của Trưởng Làng", xpTalkToChief);
                    Inventory.I.Add(db.Item("potion_red"), 2);
                    Inventory.I.Add(db.Item("potion_blue"), 1);
                    main = Main.ClearForest;
                    Begin("Dọn Dẹp Rừng Thì Thầm");
                }
                else if (main == Main.ReportBack)
                {
                    Complete("Gấu Ma Rừng Già", xpSlayBoss);
                    Inventory.I.Add(db.Item("gold"), 1);
                    Inventory.I.Add(db.Item("ring"), 1);
                    Inventory.I.Add(db.Item("potion_red"), 3);
                    main = Main.Done;
                    if (HUD.I != null) HUD.I.banner.ShowVictory("NHIỆM VỤ HOÀN THÀNH", "Làng Lá Xanh đã được cứu!");
                    AudioManager.Play("sfx_levelup", 1f, 0f);
                }
            }
            else if (npc == "girl")
            {
                if (side == Side.NotStarted && main != Main.TalkToChief)
                {
                    side = Side.Collecting;
                    Begin("Nấm Cho Bé Mai");
                }
                else if (side == Side.Collecting && Inventory.I.Count("shroom_cap") >= capGoal)
                {
                    Inventory.I.Remove(db.Item("shroom_cap"), capGoal);
                    Inventory.I.Add(db.Item("potion_green"), 3);
                    side = Side.Done;
                    Complete("Nấm Cho Bé Mai", xpMushrooms);
                }
            }
            GameEvents.RaiseQuestChanged();
        }

        void OnKilled(KillInfo k)
        {
            string id = k.id;
            if (main == Main.ClearForest)
            {
                if (id == "slime") slimes++;
                if (id == "shroom") shrooms++;
                if (slimes >= slimeGoal && shrooms >= shroomGoal)
                {
                    Complete("Dọn Dẹp Rừng Thì Thầm", xpClearForest);
                    main = Main.SlayBoss;
                    Begin("Gấu Ma Rừng Già");
                }
                GameEvents.RaiseQuestChanged();
            }
            if (id == "bear" && (main == Main.SlayBoss || main == Main.ClearForest))
            {
                main = Main.ReportBack;
                Begin("Trở Về Làng");
                GameEvents.RaiseQuestChanged();
            }
        }

        void OnItem(ItemDef item, int n)
        {
            if (item != null && item.id == "shroom_cap") GameEvents.RaiseQuestChanged();
        }

        // ------------------------------------------------------------ save
        [System.Serializable]
        class SaveState
        {
            public Main main;
            public Side side;
            public int slimes, shrooms, focus;
        }

        public string SaveKey => "quests";

        public string CaptureState() =>
            JsonUtility.ToJson(new SaveState { main = main, side = side, slimes = slimes, shrooms = shrooms, focus = focus });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<SaveState>(json);
            main = s.main;
            side = s.side;
            slimes = s.slimes;
            shrooms = s.shrooms;
            focus = s.focus;
            GameEvents.RaiseQuestChanged();
        }

        void Begin(string title)
        {
            GameEvents.RaiseLog($"Nhiệm vụ mới: {title}", Palette.LogQuest);
            if (HUD.I != null) HUD.I.banner.ShowQuest("Nhiệm vụ mới", title);
            AudioManager.Play("sfx_quest", 0.8f, 0f);
        }

        void Complete(string title, int xp)
        {
            GameEvents.RaiseLog(xp > 0 ? $"Hoàn thành: {title} (+{xp} XP)" : $"Hoàn thành: {title}", Palette.LogQuest);
            if (PlayerStats.I != null) PlayerStats.I.AddXp(xp);
            GameEvents.RaiseQuestCompleted(title);
            AudioManager.Play("sfx_levelup", 0.7f, 0f);
            var p = GameManager.I.player;
            if (p != null) VFX.Spawn("quest_complete", p.transform.position, Quaternion.identity, 1f, p.transform);
        }
    }
}
