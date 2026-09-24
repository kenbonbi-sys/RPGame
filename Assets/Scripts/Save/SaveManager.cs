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
    /// atomically with a .bak of the previous file. Every <see cref="ISaveable"/> in the scene
    /// contributes one section. Loading reloads the scene and restores the sections once every
    /// object has started.
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

        public float PlayTime { get; private set; }

        static readonly List<ISaveable> StaticSaveables = new List<ISaveable> { new Bestiary.Saveable() };

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
            if (gm == null || gm.player == null) reason = "Chưa thể lưu lúc này.";
            else if (gm.player.IsDead || gm.State == GameState.Dead) reason = "Không thể lưu khi đã gục.";
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
            var f = new SaveFile
            {
                savedAt = DateTime.Now.ToString("o"),
                playTime = PlayTime,
                level = PlayerStats.I != null ? PlayerStats.I.level : 1,
                zone = ZoneArea.Current != null ? ZoneArea.Current.zoneName : "",
                quest = CurrentQuestTitle()
            };
            foreach (var s in Saveables())
            {
                try { f.Set(s.SaveKey, s.CaptureState()); }
                catch (Exception e) { Debug.LogException(e); }
            }
            try
            {
                Write(PathFor(slot), JsonUtility.ToJson(f, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                GameEvents.RaiseLog("Lưu game thất bại!", new Color(1f, 0.4f, 0.35f));
                return false;
            }
        }

        static string CurrentQuestTitle()
        {
            var q = QuestSystem.I;
            if (q == null) return "";
            var list = q.Tracked();
            return list.Count > 0 ? list[0].title : "";
        }

        /// <summary>Writes to a temp file, then swaps it in and keeps the previous version as .bak.</summary>
        static void Write(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            string bak = path + ".bak";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            if (!File.Exists(path))
            {
                File.Move(tmp, path);
                return;
            }
            try
            {
                File.Replace(tmp, path, bak);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(path, bak, true);
                File.Delete(path);
                File.Move(tmp, path);
            }
        }

        // ------------------------------------------------------------------ load
        /// <summary>Reads a slot (falls back to its .bak if the file is damaged). Null when empty or unreadable.</summary>
        public static SaveFile Read(int slot)
        {
            string path = PathFor(slot);
            return ReadPath(path) ?? ReadPath(path + ".bak");
        }

        static SaveFile ReadPath(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var f = JsonUtility.FromJson<SaveFile>(File.ReadAllText(path));
                if (f == null || f.game != "RungThiTham" || !SaveFile.Migrate(f)) return null;
                return f;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Cannot read {path}: {e.Message}");
                return null;
            }
        }

        public static bool Exists(int slot) => File.Exists(PathFor(slot)) || File.Exists(PathFor(slot) + ".bak");

        /// <summary>Reloads the scene and restores the slot once it is running.</summary>
        public bool Load(int slot)
        {
            var f = Read(slot);
            if (f == null)
            {
                GameEvents.RaiseLog("Không đọc được file lưu.", new Color(1f, 0.4f, 0.35f));
                return false;
            }
            pending = f;
            TimeFX.Paused = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return true;
        }

        IEnumerator ApplyPending()
        {
            yield return null;   // let every Start() run first
            var f = pending;
            pending = null;
            Apply(f);
        }

        void Apply(SaveFile f)
        {
            PlayTime = f.playTime;
            foreach (var s in Saveables())
            {
                string json = f.Get(s.SaveKey);
                if (json == null) continue;
                try { s.RestoreState(json); }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            GameEvents.RaiseQuestChanged();
            GameEvents.RaiseLog("Đã tải game.", Palette.LogQuest);
        }

        static IEnumerable<ISaveable> Saveables()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (mb is ISaveable s) yield return s;
            foreach (var s in StaticSaveables) yield return s;
        }
    }
}
