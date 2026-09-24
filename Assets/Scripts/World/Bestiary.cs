using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// "Bách Khoa Trùm" — a hero's monster encyclopedia. Records creatures and the skills they
    /// use the first time the hero sees them (and logs it, like the reference), and the kills
    /// the hero shares. Each hero keeps their own book; the journal (J) shows the local one's.
    /// </summary>
    public class Bestiary : MonoBehaviour, ICharacterSaveable
    {
        public class Entry
        {
            public string id;
            public string name;
            public int kills;
            public readonly List<string> skills = new List<string>();
        }

        public const string BookName = "Bách Khoa Trùm";

        /// <summary>How close a hero must be to a boss to write down what it does.</summary>
        public const float WitnessRadius = 25f;

        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        readonly Dictionary<string, string> nameToId = new Dictionary<string, string>();
        static readonly List<PlayerController> Witnesses = new List<PlayerController>();

        public IEnumerable<Entry> All => entries.Values;
        public event System.Action Changed;

        public PlayerController Owner { get; private set; }

        /// <summary>New entries are logged on the owner's screen only.</summary>
        bool Local => Owner == null || Owner.IsLocal;

        void Awake()
        {
            Owner = GetComponent<PlayerController>();
            SaveRegistry.Register(this);
        }

        void OnDestroy() => SaveRegistry.Unregister(this);

        void OnEnable() => GameEvents.EnemyKilled += OnKilled;

        void OnDisable() => GameEvents.EnemyKilled -= OnKilled;

        void OnKilled(KillInfo k)
        {
            if (k.Credits(Owner)) RecordKill(k.id, k.name);
        }

        /// <summary>Runs <paramref name="record"/> on the book of every living hero near <paramref name="at"/>.</summary>
        public static void ForWitnesses(Vector2 at, System.Action<Bestiary> record)
        {
            Players.Within(at, WitnessRadius, Witnesses);
            foreach (var p in Witnesses)
                if (p.bestiary != null) record(p.bestiary);
        }

        Entry Get(string id, string name)
        {
            if (!entries.TryGetValue(id, out var e))
            {
                e = new Entry { id = id, name = name };
                entries[id] = e;
                nameToId[name] = id;
                if (Local) GameEvents.RaiseLog($"{BookName}: ghi lại quái vật \"{name}\".", Palette.LogBestiary);
            }
            return e;
        }

        public void RecordSeen(string id, string name)
        {
            Get(id, name);
            Changed?.Invoke();
        }

        public void RecordKill(string id, string name)
        {
            Get(id, name).kills++;
            Changed?.Invoke();
        }

        public void RecordSkill(string monsterName, string skill)
        {
            string id = nameToId.TryGetValue(monsterName, out var found) ? found : monsterName;
            var e = Get(id, monsterName);
            if (e.skills.Contains(skill)) return;
            e.skills.Add(skill);
            if (Local) GameEvents.RaiseLog($"{BookName}: ghi lại kỹ năng \"{skill}\" của {monsterName}.", Palette.LogBestiary);
            Changed?.Invoke();
        }

        public Entry Find(string id) => entries.TryGetValue(id, out var e) ? e : null;

        // ------------------------------------------------------------------ save
        [System.Serializable]
        class SaveState
        {
            public List<EntryState> entries = new List<EntryState>();
        }

        [System.Serializable]
        class EntryState
        {
            public string id;
            public string name;
            public int kills;
            public List<string> skills = new List<string>();
        }

        public string SaveKey => "bestiary";

        public string CaptureState()
        {
            var s = new SaveState();
            foreach (var e in entries.Values)
                s.entries.Add(new EntryState { id = e.id, name = e.name, kills = e.kills, skills = new List<string>(e.skills) });
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<SaveState>(json);
            entries.Clear();
            nameToId.Clear();
            foreach (var st in s.entries)
            {
                var e = new Entry { id = st.id, name = st.name, kills = st.kills };
                e.skills.AddRange(st.skills);
                entries[e.id] = e;
                nameToId[e.name] = e.id;
            }
            Changed?.Invoke();
        }
    }
}
