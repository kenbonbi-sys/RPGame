using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPG
{
    /// <summary>
    /// Save v1 (plan §16): 3 manual slots + 1 autosave, JSON with a version number, written
    /// atomically with a .bak of the previous file. Every <see cref="ISaveable"/> contributes one
    /// section: the world's (bosses, day/night, world objects) and the local hero's
    /// (<see cref="ICharacterSaveable"/>: stats, bag, quests, Bách Khoa Trùm, Yarn variables).
    /// <see cref="CaptureCharacter"/> / <see cref="RestoreCharacter"/> handle one hero on their
    /// own, which is what an online server keeps per character. Loading reloads the scene and
    /// restores the sections once every object has started.
    /// Autosave: after a boss or mini-boss, after finishing a quest and every few minutes, but
    /// only out of combat.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager I { get; private set; }

        public const int AutoSlot = 0;
        public const int SlotCount = 3;   // manual slots are 1..SlotCount

        [Header("Autosave")]
        public float autosaveInterval = 300f;
        [Tooltip("Seconds without dealing or taking damage before an autosave may happen.")]
        public float outOfCombatDelay = 10f;

        /// <summary>A file waiting to be applied after the scene reload.</summary>
        static SaveFile pending;
        public static bool HasPendingLoad => pending != null;
        /// <summary>Zone of the save being loaded (the scene loader opens it first).</summary>
        public static string PendingZoneId => pending != null ? pending.zoneId : null;

        public float PlayTime { get; private set; }

        static readonly List<ISaveable> StaticSaveables = new List<ISaveable> { new WorldStateStore() };

        float nextAutosave;
        float lastCombat = -999f;
        bool autosaveQueued;
        float autosaveAt;

        /// <summary>Tests point this at a temp folder so they never touch real saves.</summary>
        public static string FolderOverride;

        public static string Folder => !string.IsNullOrEmpty(FolderOverride) ? FolderOverride : Path.Combine(Application.persistentDataPath, "saves");

        public static string PathFor(int slot) => Path.Combine(Folder, slot == AutoSlot ? "autosave.json" : $"slot{slot}.json");

        void Awake() => I = this;

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void OnEnable()
        {
            GameEvents.Damaged += OnDamaged;
            GameEvents.EnemyKilled += OnKilled;
            GameEvents.QuestCompleted += OnQuestCompleted;
        }

        void OnDisable()
        {
            GameEvents.Damaged -= OnDamaged;
            GameEvents.EnemyKilled -= OnKilled;
            GameEvents.QuestCompleted -= OnQuestCompleted;
        }

        void Start()
        {
            nextAutosave = Time.unscaledTime + autosaveInterval;
            if (pending != null) StartCoroutine(ApplyPending());
        }

        void Update()
        {
            if (!TimeFX.Paused) PlayTime += Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            if ((autosaveQueued && now >= autosaveAt) || now >= nextAutosave)
            {
                if (CanAutosave())
                {
                    autosaveQueued = false;
                    nextAutosave = now + autosaveInterval;
                    if (Save(AutoSlot)) GameEvents.RaiseLog("Đã tự động lưu.", new Color(0.7f, 0.68f, 0.76f));
                }
            }
        }

        // ------------------------------------------------------------------ triggers
        void OnDamaged(Health h, DamageInfo d, float amount)
        {
            if (h.team == Team.Player || d.sourceTeam == Team.Player) lastCombat = Time.unscaledTime;
        }

        void OnKilled(KillInfo k)
        {
            if (k.rank >= EnemyRank.MiniBoss) RequestAutosave(3f);
        }

        void OnQuestCompleted(string title) => RequestAutosave(1f);

        /// <summary>Autosaves as soon as possible after <paramref name="delay"/> seconds (waits until out of combat).</summary>
        public void RequestAutosave(float delay)
        {
            autosaveQueued = true;
            autosaveAt = Time.unscaledTime + delay;
        }

        bool CanAutosave()
        {
            if (AutoShot.Active) return false;   // test runs never touch real saves
            return CanSave(out _) && Time.unscaledTime - lastCombat > outOfCombatDelay;
        }

        /// <summary>Saving is allowed while playing or in a menu, never while dead, talking or in a cut-scene.</summary>
        public bool CanSave(out string reason)
        {
            reason = null;
            var gm = GameManager.I;
            var me = Players.Local;
            if (GameSession.Mode != SessionMode.Offline) reason = "Chơi online: máy chủ tự lưu nhân vật của bạn.";
            else if (gm == null || me == null) reason = "Chưa thể lưu lúc này.";
            else if (me.IsDead || gm.State == GameState.Dead) reason = "Không thể lưu khi đã gục.";
            else if (gm.State == GameState.Dialogue || gm.State == GameState.Cinematic) reason = "Không thể lưu lúc này.";
            return reason == null;
        }

        // ------------------------------------------------------------------ save
        public bool Save(int slot)
        {
            if (!CanSave(out string why))
            {
                GameEvents.RaiseLog(why, new Color(1f, 0.6f, 0.5f));
                return false;
            }
            var me = Players.Local;
            var f = new SaveFile
            {
                savedAt = DateTime.Now.ToString("o"),
                playTime = PlayTime,
                level = me.stats != null ? me.stats.level : 1,
                zone = ZoneArea.Current != null ? ZoneArea.Current.zoneName : "",
                zoneId = ZoneRoot.Current != null && ZoneRoot.Current.def != null ? ZoneRoot.Current.def.id : "",
                quest = CurrentQuestTitle(me)
            };
            foreach (var s in CaptureWorld()) f.Set(s.key, s.json);
            foreach (var s in CaptureCharacter(me)) f.Set(s.key, s.json);
            try
            {
                SafeFile.Write(PathFor(slot), JsonUtility.ToJson(f, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                GameEvents.RaiseLog("Lưu game thất bại!", new Color(1f, 0.4f, 0.35f));
                return false;
            }
        }

        /// <summary>The first quest of a hero's tracker (shown in save lists).</summary>
        public static string CurrentQuestTitle(PlayerController hero)
        {
            var q = hero != null ? hero.quests : null;
            if (q == null) return "";
            var list = q.Tracked();
            return list.Count > 0 ? list[0].title : "";
        }

        // ------------------------------------------------------------------ load
        /// <summary>Reads a slot (falls back to its .bak if the file is damaged). Null when empty or unreadable.</summary>
        public static SaveFile Read(int slot) => ReadFile(PathFor(slot));

        /// <summary>Reads a save file (or its .bak), upgrading older versions. Null when missing or unreadable.</summary>
        public static SaveFile ReadFile(string path)
        {
            SaveFile result = null;
            SafeFile.Read(path, text =>
            {
                try
                {
                    var f = JsonUtility.FromJson<SaveFile>(text);
                    if (f == null || f.game != "RungThiTham" || !SaveFile.Migrate(f)) return false;
                    result = f;
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Save] Cannot read {path}: {e.Message}");
                    return false;
                }
            });
            return result;
        }

        public static bool Exists(int slot) => SafeFile.Exists(PathFor(slot));

        /// <summary>Reloads the scene and restores the slot once it is running.</summary>
        public bool Load(int slot)
        {
            if (GameSession.Mode != SessionMode.Offline)
            {
                GameEvents.RaiseLog("Chơi online không tải được file lưu offline: nhân vật online nằm trên máy chủ.", new Color(1f, 0.6f, 0.5f));
                return false;
            }
            var f = Read(slot);
            if (f == null)
            {
                GameEvents.RaiseLog("Không đọc được file lưu.", new Color(1f, 0.4f, 0.35f));
                return false;
            }
            pending = f;
            TimeFX.Paused = false;
            SceneManager.LoadScene(ZoneRoot.CoreScene, LoadSceneMode.Single);   // Core boots, then loads the saved zone
            return true;
        }

        IEnumerator ApplyPending()
        {
            yield return null;   // let every Start() run first
            while (SceneLoader.I != null && SceneLoader.I.Busy) yield return null;   // and the saved zone load
            yield return null;
            var f = pending;
            pending = null;
            Apply(f);
        }

        void Apply(SaveFile f)
        {
            PlayTime = f.playTime;
            RestoreWorld(f.sections);
            RestoreCharacter(Players.Local, f.sections);
            if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            GameEvents.RaiseQuestChanged();
            GameEvents.RaiseLog("Đã tải game.", Palette.LogQuest);
        }

        // ------------------------------------------------------------------ world and characters
        /// <summary>The world's sections: bosses, day and night, world objects (online: the zone server's).</summary>
        public static List<SaveSection> CaptureWorld()
        {
            var sections = new List<SaveSection>();
            foreach (var s in WorldSaveables()) Capture(s, sections);
            return sections;
        }

        /// <summary>One hero's sections: stats, bag, quests, Bách Khoa Trùm, Yarn variables (online: stored per character).</summary>
        public static List<SaveSection> CaptureCharacter(PlayerController hero)
        {
            var sections = new List<SaveSection>();
            foreach (var s in CharacterSaveables(hero)) Capture(s, sections);
            return sections;
        }

        /// <summary>Puts the world's sections back; sections of heroes are skipped.</summary>
        public static void RestoreWorld(List<SaveSection> sections)
        {
            foreach (var s in WorldSaveables()) Restore(s, sections);
        }

        /// <summary>Puts one hero's sections back (the hero's own section first: stats set max HP and quest unlocks).</summary>
        public static void RestoreCharacter(PlayerController hero, List<SaveSection> sections)
        {
            foreach (var s in CharacterSaveables(hero)) Restore(s, sections);
        }

        static void Capture(ISaveable s, List<SaveSection> into)
        {
            try { into.Add(new SaveSection { key = s.SaveKey, json = s.CaptureState() }); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void Restore(ISaveable s, List<SaveSection> sections)
        {
            string json = null;
            foreach (var section in sections)
                if (section.key == s.SaveKey) json = section.json;
            if (json == null) return;
            try { s.RestoreState(json); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static IEnumerable<ISaveable> WorldSaveables()
        {
            foreach (var s in new List<ISaveable>(SaveRegistry.Items))
                if (!(s is ICharacterSaveable)) yield return s;
            foreach (var s in StaticSaveables) yield return s;
        }

        static List<ISaveable> CharacterSaveables(PlayerController hero)
        {
            var list = new List<ISaveable>();
            if (hero == null) return list;
            list.Add(hero);
            foreach (var s in SaveRegistry.Items)
                if (s is ICharacterSaveable c && c.Owner == hero && !ReferenceEquals(s, hero)) list.Add(s);
            return list;
        }
    }
}
