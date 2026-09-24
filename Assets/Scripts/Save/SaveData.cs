using System;
using System.Collections.Generic;

namespace RPG
{
    /// <summary>
    /// Anything that keeps state across sessions. Each saveable writes its own JSON section
    /// under a stable key, so systems can be added or removed without breaking old saves.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Stable, unique id of the section ("player", "inventory", "boss:bear"…).</summary>
        string SaveKey { get; }
        string CaptureState();
        void RestoreState(string json);
    }

    /// <summary>
    /// A saveable that belongs to one hero (stats, bag, quests, Bách Khoa Trùm…) rather than to
    /// the world. The offline save file keeps the local hero's sections next to the world's;
    /// online, each character's sections are stored on their own (Docs/KeHoach-Online.md).
    /// </summary>
    public interface ICharacterSaveable : ISaveable
    {
        PlayerController Owner { get; }
    }

    [Serializable]
    public class SaveSection
    {
        public string key;
        public string json;
    }

    /// <summary>One save file (JSON). Bump <see cref="CurrentVersion"/> and add a step to Migrate when the format changes.</summary>
    [Serializable]
    public class SaveFile
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string game = "RungThiTham";
        public string savedAt;       // ISO 8601, local time
        public float playTime;       // seconds
        // summary shown in the slot list without restoring anything
        public int level;
        public string zone;          // display name
        public string zoneId;        // ZoneDef id: which zone scene to load
        public string quest;
        /// <summary>Online: the character's name (the server keeps one file per character). Empty offline.</summary>
        public string character;
        public List<SaveSection> sections = new List<SaveSection>();

        public string Get(string key)
        {
            foreach (var s in sections)
                if (s.key == key) return s.json;
            return null;
        }

        public void Set(string key, string json)
        {
            foreach (var s in sections)
            {
                if (s.key != key) continue;
                s.json = json;
                return;
            }
            sections.Add(new SaveSection { key = key, json = json });
        }

        /// <summary>
        /// Upgrades an older file in place, one version at a time, so patches never break saves.
        /// Returns false for files from a newer build than this one.
        /// </summary>
        public static bool Migrate(SaveFile f)
        {
            if (f == null || f.version > CurrentVersion) return false;
            // v1 is the first format. Future example:
            // if (f.version == 1) { ...rename or convert sections...; f.version = 2; }
            f.version = CurrentVersion;
            return true;
        }
    }
}
