using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// "Bách Khoa Trùm" — the monster encyclopedia. Records creatures and the skills
    /// they use the first time the player sees them (and logs it, like the reference).
    /// </summary>
    public static class Bestiary
    {
        public class Entry
        {
            public string id;
            public string name;
            public int kills;
            public readonly List<string> skills = new List<string>();
        }

        public const string BookName = "Bách Khoa Trùm";
        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        static readonly Dictionary<string, string> NameToId = new Dictionary<string, string>();

        public static IEnumerable<Entry> All => Entries.Values;
        public static event System.Action Changed;

        public static void Reset()
        {
            Entries.Clear();
            NameToId.Clear();
            Changed = null;
        }

        static Entry Get(string id, string name)
        {
            if (!Entries.TryGetValue(id, out var e))
            {
                e = new Entry { id = id, name = name };
                Entries[id] = e;
                NameToId[name] = id;
                GameEvents.RaiseLog($"{BookName}: ghi lại quái vật \"{name}\".", Palette.LogBestiary);
            }
            return e;
        }

        public static void RecordSeen(string id, string name)
        {
            Get(id, name);
            Changed?.Invoke();
        }

        public static void RecordKill(string id, string name)
        {
            Get(id, name).kills++;
            Changed?.Invoke();
        }

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

        /// <summary>Saves the static bestiary through the SaveManager.</summary>
        public class Saveable : ISaveable
        {
            public string SaveKey => "bestiary";

            public string CaptureState()
            {
                var s = new SaveState();
                foreach (var e in Entries.Values)
                    s.entries.Add(new EntryState { id = e.id, name = e.name, kills = e.kills, skills = new List<string>(e.skills) });
                return JsonUtility.ToJson(s);
            }

            public void RestoreState(string json)
            {
                var s = JsonUtility.FromJson<SaveState>(json);
                Entries.Clear();
                NameToId.Clear();
                foreach (var st in s.entries)
                {
                    var e = new Entry { id = st.id, name = st.name, kills = st.kills };
                    e.skills.AddRange(st.skills);
                    Entries[e.id] = e;
                    NameToId[e.name] = e.id;
                }
                Changed?.Invoke();
            }
        }

        public static void RecordSkill(string monsterName, string skill)
        {
            string id = NameToId.TryGetValue(monsterName, out var found) ? found : monsterName;
            var e = Get(id, monsterName);
            if (e.skills.Contains(skill)) return;
            e.skills.Add(skill);
            GameEvents.RaiseLog($"{BookName}: ghi lại kỹ năng \"{skill}\" của {monsterName}.", Palette.LogBestiary);
            Changed?.Invoke();
        }
    }
}
