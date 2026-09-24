using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Runs the <see cref="QuestDef"/> assets (plan §09): unlocks, objective progress from game
    /// events, rewards, flags and follow-up quests. Dialogue (Yarn) starts and turns quests in
    /// through <see cref="StartQuest"/> / <see cref="CompleteQuest"/>; everything else is driven
    /// by kills, items, conversations, zones and scripted reports.
    /// </summary>
    public class QuestSystem : MonoBehaviour, ISaveable
    {
        public static QuestSystem I { get; private set; }

        public enum Marker { None, Exclaim, Question }

        [Tooltip("Empty = every quest of the GameDatabase.")]
        public List<QuestDef> quests = new List<QuestDef>();

        /// <summary>Which tracked quest is shown first in the HUD (Tab cycles).</summary>
        public int focus;

        class State
        {
            public QuestDef def;
            public QuestStatus status;
            public int[] progress;
        }

        readonly List<State> states = new List<State>();
        readonly Dictionary<string, State> byId = new Dictionary<string, State>();
        readonly HashSet<string> flags = new HashSet<string>();
        readonly Dictionary<string, int> kills = new Dictionary<string, int>();
        Inventory boundInventory;
        string talkingTo;   // npc in conversation right now

        void Awake()
        {
            I = this;
            Build();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            if (boundInventory != null) boundInventory.Changed -= OnInventoryChanged;
        }

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnKilled;
            GameEvents.DialogueStarted += OnDialogueStarted;
            GameEvents.DialogueEnded += OnDialogueEnded;
            GameEvents.ZoneEntered += OnZoneEntered;
            GameEvents.LevelUp += OnLevelUp;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnKilled;
            GameEvents.DialogueStarted -= OnDialogueStarted;
            GameEvents.DialogueEnded -= OnDialogueEnded;
            GameEvents.ZoneEntered -= OnZoneEntered;
            GameEvents.LevelUp -= OnLevelUp;
        }

        void Start()
        {
            boundInventory = Inventory.I;
            if (boundInventory != null) boundInventory.Changed += OnInventoryChanged;
            RefreshUnlocks(false);
        }

        void Build()
        {
            states.Clear();
            byId.Clear();
            var list = quests;
            if (list == null || list.Count == 0)
            {
                var db = GameManager.I != null ? GameManager.I.db : null;
                list = db != null ? db.quests : new List<QuestDef>();
            }
            foreach (var def in list)
            {
                if (def == null || string.IsNullOrEmpty(def.id) || byId.ContainsKey(def.id)) continue;
                var s = new State { def = def, status = QuestStatus.Locked, progress = new int[def.objectives.Count] };
                states.Add(s);
                byId[def.id] = s;
            }
        }

        // ------------------------------------------------------------ queries
        public QuestDef Def(string id) => byId.TryGetValue(id, out var s) ? s.def : null;

        public QuestStatus Status(string id) => byId.TryGetValue(id, out var s) ? s.status : QuestStatus.Locked;

        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        public int KillCount(string enemyId) => kills.TryGetValue(enemyId, out int n) ? n : 0;

        /// <summary>Current progress of one objective (Collect / Deliver read the bag).</summary>
        public int Progress(string id, int objective)
        {
            if (!byId.TryGetValue(id, out var s) || objective < 0 || objective >= s.progress.Length) return 0;
            return Progress(s, objective);
        }

        /// <summary>How many are still missing for one objective.</summary>
        public int Remaining(string id, int objective)
        {
            if (!byId.TryGetValue(id, out var s) || objective < 0 || objective >= s.progress.Length) return 0;
            return Mathf.Max(0, s.def.objectives[objective].count - Progress(s, objective));
        }

        int Progress(State s, int i)
        {
            var o = s.def.objectives[i];
            if (s.status == QuestStatus.Done) return o.count;
            if (o.kind == ObjectiveKind.Collect || o.kind == ObjectiveKind.Deliver)
                return Mathf.Min(o.count, Inventory.I != null ? Inventory.I.Count(o.target) : 0);
            return Mathf.Min(o.count, s.progress[i]);
        }

        bool ObjectiveDone(State s, int i) => Progress(s, i) >= s.def.objectives[i].count;

        bool AllDone(State s)
        {
            for (int i = 0; i < s.progress.Length; i++)
                if (!ObjectiveDone(s, i)) return false;
            return true;
        }

        // ------------------------------------------------------------ lifecycle
        bool Unlocked(QuestDef d)
        {
            int level = PlayerStats.I != null ? PlayerStats.I.level : 1;
            if (level < d.minLevel) return false;
            foreach (var r in d.requires)
                if (r != null && Status(r.id) != QuestStatus.Done) return false;
            foreach (var f in d.requiredFlags)
                if (!HasFlag(f)) return false;
            return true;
        }

        /// <summary>Locked quests whose conditions are met become available (or start, if autoStart).</summary>
        void RefreshUnlocks(bool announce)
        {
            bool changed = false;
            foreach (var s in states)
            {
                if (s.status != QuestStatus.Locked || !Unlocked(s.def)) continue;
                s.status = QuestStatus.Available;
                changed = true;
                if (s.def.autoStart) Begin(s, announce);
            }
            if (changed) GameEvents.RaiseQuestChanged();
        }

        /// <summary>Accepts an available quest (Yarn: &lt;&lt;quest_start id&gt;&gt;).</summary>
        public bool StartQuest(string id)
        {
            if (!byId.TryGetValue(id, out var s))
            {
                Debug.LogWarning("[Quest] Unknown quest: " + id);
                return false;
            }
            if (s.status == QuestStatus.Locked && Unlocked(s.def)) s.status = QuestStatus.Available;
            if (s.status != QuestStatus.Available) return false;
            Begin(s, true);
            return true;
        }

        void Begin(State s, bool announce)
        {
            s.status = QuestStatus.Active;
            for (int i = 0; i < s.progress.Length; i++)
            {
                var o = s.def.objectives[i];
                s.progress[i] = o.kind == ObjectiveKind.Kill && o.countPrevious ? KillCount(o.target) : 0;
            }
            if (announce)
            {
                GameEvents.RaiseLog($"Nhiệm vụ mới: {s.def.title}", Palette.LogQuest);
                if (HUD.I != null) HUD.I.banner.ShowQuest("Nhiệm vụ mới", s.def.title);
                AudioManager.Play("sfx_quest", 0.8f, 0f);
            }
            Evaluate(s);
            GameEvents.RaiseQuestChanged();
        }

        /// <summary>Turns in a ready quest: hands over Deliver items, gives rewards, sets flags, starts follow-ups.</summary>
        public bool CompleteQuest(string id)
        {
            if (!byId.TryGetValue(id, out var s)) return false;
            if (s.status != QuestStatus.Active && s.status != QuestStatus.Ready) return false;
            if (!AllDone(s)) return false;
            var db = GameManager.I != null ? GameManager.I.db : null;
            var inv = Inventory.I;
            foreach (var o in s.def.objectives)
                if (o.kind == ObjectiveKind.Deliver && inv != null && db != null) inv.Remove(db.Item(o.target), o.count);
            s.status = QuestStatus.Done;

            GameEvents.RaiseLog(s.def.xp > 0 ? $"Hoàn thành: {s.def.title} (+{s.def.xp} XP)" : $"Hoàn thành: {s.def.title}", Palette.LogQuest);
            AudioManager.Play("sfx_levelup", 0.7f, 0f);
            var p = GameManager.I != null ? GameManager.I.player : null;
            if (p != null) VFX.Spawn("quest_complete", p.transform.position, Quaternion.identity, 1f, p.transform);
            if (inv != null)
            {
                if (s.def.gold > 0)
                {
                    inv.gold += s.def.gold;
                    GameEvents.RaiseLog($"Nhận được {s.def.gold} vàng", Palette.Gold);
                }
                foreach (var r in s.def.items)
                    if (r != null && r.item != null) inv.Add(r.item, r.count);
            }
            if (PlayerStats.I != null) PlayerStats.I.AddXp(s.def.xp);
            foreach (var f in s.def.setFlags) SetFlag(f, false);
            GameEvents.RaiseQuestCompleted(s.def.title);

            RefreshUnlocks(true);
            foreach (var next in s.def.followUps)
                if (next != null && Status(next.id) == QuestStatus.Available) Begin(byId[next.id], true);
            GameEvents.RaiseQuestChanged();
            return true;
        }

        void Evaluate(State s)
        {
            if (s.status == QuestStatus.Active && AllDone(s))
            {
                if (string.IsNullOrEmpty(s.def.turnIn))
                {
                    CompleteQuest(s.def.id);
                    return;
                }
                s.status = QuestStatus.Ready;
                // no "report to the chief" while already talking to the chief
                if (s.def.turnIn != talkingTo) GameEvents.RaiseLog($"{s.def.title}: {TurnInText(s)}", Palette.LogQuest);
                GameEvents.RaiseQuestChanged();
            }
            else if (s.status == QuestStatus.Ready && !AllDone(s))
            {
                s.status = QuestStatus.Active;   // e.g. the collected items were used up
                GameEvents.RaiseQuestChanged();
            }
        }

        public void SetFlag(string flag, bool refresh = true)
        {
            if (string.IsNullOrEmpty(flag) || !flags.Add(flag)) return;
            if (refresh) RefreshUnlocks(true);
        }

        // ------------------------------------------------------------ progress sources
        /// <summary>Adds progress to every active objective of a kind whose target matches.</summary>
        void Advance(ObjectiveKind kind, string target, int amount)
        {
            if (string.IsNullOrEmpty(target)) return;
            bool changed = false;
            foreach (var s in states.ToArray())
            {
                if (s.status != QuestStatus.Active) continue;
                for (int i = 0; i < s.progress.Length; i++)
                {
                    var o = s.def.objectives[i];
                    if (o.kind != kind || o.target != target || s.progress[i] >= o.count) continue;
                    s.progress[i] = Mathf.Min(o.count, s.progress[i] + amount);
                    changed = true;
                }
                Evaluate(s);
            }
            if (changed) GameEvents.RaiseQuestChanged();
        }

        void OnKilled(KillInfo k)
        {
            kills[k.id] = KillCount(k.id) + 1;
            Advance(ObjectiveKind.Kill, k.id, 1);
        }

        void OnDialogueStarted(string npcId)
        {
            talkingTo = npcId;
            Advance(ObjectiveKind.Talk, npcId, int.MaxValue / 2);
        }

        void OnDialogueEnded(string npcId) => talkingTo = null;

        void OnZoneEntered(string zone) => Advance(ObjectiveKind.Reach, zone, int.MaxValue / 2);

        void OnLevelUp(int level) => RefreshUnlocks(true);

        void OnInventoryChanged()
        {
            foreach (var s in states.ToArray())
                if (s.status == QuestStatus.Active || s.status == QuestStatus.Ready) Evaluate(s);
            GameEvents.RaiseQuestChanged();
        }

        /// <summary>A trigger reached a place id (Reach objectives).</summary>
        public void NotifyReached(string placeId) => Advance(ObjectiveKind.Reach, placeId, int.MaxValue / 2);

        /// <summary>The player used a world object (Interact objectives).</summary>
        public void NotifyInteract(string objectId) => Advance(ObjectiveKind.Interact, objectId, 1);

        /// <summary>Escort / Defend / Survive encounter scripts report progress here.</summary>
        public void Report(ObjectiveKind kind, string target, int amount = 1) => Advance(kind, target, amount);

        // ------------------------------------------------------------ tracker & markers
        public struct Entry
        {
            public string id;
            public string title;
            public string objective;
            public bool main;
        }

        static string NpcName(string npcId)
        {
            var n = NPC.All.Find(x => x.npcId == npcId);
            return n != null ? n.displayName : npcId;
        }

        string TurnInText(State s) =>
            !string.IsNullOrEmpty(s.def.turnInText) ? s.def.turnInText : $"Báo lại cho {NpcName(s.def.turnIn)}";

        string ObjectiveText(State s)
        {
            var parts = new List<string>();
            for (int i = 0; i < s.progress.Length; i++)
            {
                var o = s.def.objectives[i];
                string t = string.IsNullOrEmpty(o.text) ? o.target : o.text;
                if (o.count > 1) t += $" ({Progress(s, i)}/{o.count})";
                parts.Add(ObjectiveDone(s, i) ? $"<color=#9a93a8>{t}</color>" : t);
            }
            return string.Join(" · ", parts);
        }

        /// <summary>Quests shown in the HUD tracker: main first, then side; available ones with a hint.</summary>
        public List<Entry> Tracked()
        {
            var list = new List<Entry>();
            foreach (bool mainPass in new[] { true, false })
            {
                foreach (var s in states)
                {
                    if ((s.def.kind == QuestKind.Main) != mainPass) continue;
                    string objective = null;
                    if (s.status == QuestStatus.Ready) objective = TurnInText(s);
                    else if (s.status == QuestStatus.Active) objective = ObjectiveText(s);
                    else if (s.status == QuestStatus.Available && !string.IsNullOrEmpty(s.def.availableText)) objective = s.def.availableText;
                    if (objective != null) list.Add(new Entry { id = s.def.id, title = s.def.title, objective = objective, main = mainPass });
                }
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

        /// <summary>"?" over whoever takes a ready quest, "!" over givers of available quests and Talk targets.</summary>
        public Marker MarkerFor(string npc, out QuestKind kind)
        {
            kind = QuestKind.Main;
            foreach (var s in states)
            {
                if (s.status == QuestStatus.Ready && s.def.turnIn == npc)
                {
                    kind = s.def.kind;
                    return Marker.Question;
                }
            }
            foreach (var s in states)
            {
                bool offer = s.status == QuestStatus.Available && !s.def.autoStart && s.def.giver == npc;
                bool talk = false;
                if (s.status == QuestStatus.Active)
                    for (int i = 0; i < s.progress.Length; i++)
                        if (s.def.objectives[i].kind == ObjectiveKind.Talk && s.def.objectives[i].target == npc && !ObjectiveDone(s, i))
                            talk = true;
                if (offer || talk)
                {
                    kind = s.def.kind;
                    return Marker.Exclaim;
                }
            }
            return Marker.None;
        }

        public Marker MarkerFor(string npc) => MarkerFor(npc, out _);

        /// <summary>Where the focused objective is (for the minimap star).</summary>
        public Vector3? ObjectivePosition()
        {
            var list = Tracked();
            if (list.Count == 0 || !byId.TryGetValue(list[Mathf.Clamp(focus, 0, list.Count - 1)].id, out var s)) return null;
            if (s.status == QuestStatus.Ready) return Locate(s.def.turnIn);
            if (s.status == QuestStatus.Available) return Locate(s.def.giver);
            for (int i = 0; i < s.progress.Length; i++)
            {
                if (ObjectiveDone(s, i)) continue;
                var o = s.def.objectives[i];
                return Locate(string.IsNullOrEmpty(o.marker) ? o.target : o.marker);
            }
            return null;
        }

        static Vector3? Locate(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var n = NPC.All.Find(x => x.npcId == id);
            if (n != null) return n.transform.position;
            var gm = GameManager.I;
            var t = gm != null ? gm.Spot(id) : null;
            return t != null ? t.position : (Vector3?)null;
        }

        // ------------------------------------------------------------ save
        [Serializable]
        class QuestState
        {
            public string id;
            public QuestStatus status;
            public int[] progress;
        }

        [Serializable]
        class KillCountState
        {
            public string id;
            public int count;
        }

        [Serializable]
        class SaveState
        {
            public List<QuestState> quests = new List<QuestState>();
            public List<string> flags = new List<string>();
            public List<KillCountState> kills = new List<KillCountState>();
            public int focus;
        }

        public string SaveKey => "quests";

        public string CaptureState()
        {
            var st = new SaveState { focus = focus };
            foreach (var s in states) st.quests.Add(new QuestState { id = s.def.id, status = s.status, progress = (int[])s.progress.Clone() });
            st.flags.AddRange(flags);
            foreach (var kv in kills) st.kills.Add(new KillCountState { id = kv.Key, count = kv.Value });
            return JsonUtility.ToJson(st);
        }

        public void RestoreState(string json)
        {
            var st = JsonUtility.FromJson<SaveState>(json);
            flags.Clear();
            flags.UnionWith(st.flags);
            kills.Clear();
            foreach (var k in st.kills) kills[k.id] = k.count;
            foreach (var s in states)
            {
                s.status = QuestStatus.Locked;
                Array.Clear(s.progress, 0, s.progress.Length);
            }
            foreach (var q in st.quests)
            {
                if (!byId.TryGetValue(q.id, out var s)) continue;   // quest removed since the save
                s.status = q.status;
                if (q.progress != null)
                    for (int i = 0; i < s.progress.Length && i < q.progress.Length; i++) s.progress[i] = q.progress[i];
            }
            focus = st.focus;
            RefreshUnlocks(false);   // quests added since the save
            GameEvents.RaiseQuestChanged();
        }
    }
}
