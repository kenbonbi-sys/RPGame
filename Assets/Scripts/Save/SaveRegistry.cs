using System.Collections.Generic;

namespace RPG
{
    /// <summary>
    /// Every live <see cref="ISaveable"/>. Components register in Awake and leave in OnDestroy
    /// (not OnEnable/OnDisable, so a defeated boss that is switched off still saves its state).
    /// </summary>
    public static class SaveRegistry
    {
        static readonly List<ISaveable> All = new List<ISaveable>();

        public static IReadOnlyList<ISaveable> Items => All;

        public static void Register(ISaveable s)
        {
            if (s != null && !All.Contains(s)) All.Add(s);
        }

        public static void Unregister(ISaveable s) => All.Remove(s);
    }
}
