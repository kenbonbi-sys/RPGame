using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>A world object whose state is saved (opened chest, open door, used lever, shortcut).</summary>
    public interface IWorldState
    {
        string CaptureWorldState();
        void RestoreWorldState(string json);
    }

    /// <summary>
    /// Stable id of a placed world object (plan §16: chests, doors, bosses, shortcuts). Assigned
    /// once in the editor and kept in the scene file, so saves find the same object in every build.
    /// Duplicating an object in the editor gives the copy a new id. Components on the same
    /// GameObject that implement <see cref="IWorldState"/> are saved under this id.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldId : MonoBehaviour
    {
        [SerializeField] string id;

        public string Id => id;

        static readonly Dictionary<string, WorldId> Live = new Dictionary<string, WorldId>();

        public static WorldId Find(string worldId) => worldId != null && Live.TryGetValue(worldId, out var w) ? w : null;

        public static IEnumerable<WorldId> All => Live.Values;

        void Awake()
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[WorldId] {name} has no id; its state will not be saved.", this);
                return;
            }
            if (Live.TryGetValue(id, out var other) && other != null && other != this)
                Debug.LogError($"[WorldId] Duplicate id {id} on {name} and {other.name}.", this);
            Live[id] = this;
        }

        void OnDestroy()
        {
            if (!string.IsNullOrEmpty(id) && Live.TryGetValue(id, out var w) && w == this) Live.Remove(id);
        }

        /// <summary>For code that builds scenes: set a readable id ("chest_forest_01").</summary>
        public void SetId(string newId) => id = newId;

#if UNITY_EDITOR
        void Reset() => id = NewId();

        void OnValidate()
        {
            // prefab assets carry no id; each placed instance gets its own
            if (UnityEditor.EditorUtility.IsPersistent(this) || !gameObject.scene.IsValid())
            {
                id = "";
                return;
            }
            if (string.IsNullOrEmpty(id) || HasDuplicateInScene()) id = NewId();
        }

        /// <summary>True when an object earlier in the hierarchy already holds this id (this one is the copy).</summary>
        bool HasDuplicateInScene()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var w in root.GetComponentsInChildren<WorldId>(true))
                {
                    if (w == this) return false;
                    if (w.id == id) return true;
                }
            return false;
        }

        static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 12);
#endif
    }

    /// <summary>Saves every <see cref="IWorldState"/> component of the live <see cref="WorldId"/> objects.</summary>
    public class WorldStateStore : ISaveable
    {
        [Serializable]
        class Entry
        {
            public string id;
            public string type;
            public string json;
        }

        [Serializable]
        class SaveState
        {
            public List<Entry> entries = new List<Entry>();
        }

        public string SaveKey => "world";

        public string CaptureState()
        {
            var st = new SaveState();
            foreach (var w in WorldId.All)
            {
                if (w == null) continue;
                foreach (var c in w.GetComponents<IWorldState>())
                    st.entries.Add(new Entry { id = w.Id, type = c.GetType().Name, json = c.CaptureWorldState() });
            }
            return JsonUtility.ToJson(st);
        }

        public void RestoreState(string json)
        {
            var st = JsonUtility.FromJson<SaveState>(json);
            foreach (var e in st.entries)
            {
                var w = WorldId.Find(e.id);
                if (w == null) continue;   // object removed since the save
                foreach (var c in w.GetComponents<IWorldState>())
                    if (c.GetType().Name == e.type) c.RestoreWorldState(e.json);
            }
        }
    }
}
