using UnityEngine;

namespace RPG
{
    /// <summary>
    /// One region of the world (plan §10): its scene, level range, music and ambience.
    /// Every zone is its own scene loaded next to the Core scene, so zones can be built in parallel.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Zone")]
    public class ZoneDef : ScriptableObject
    {
        public string id;
        public string displayName;
        [Tooltip("Scene file name (without .unity); the scene must be in the build settings.")]
        public string sceneName;
        public int levelMin = 1;
        public int levelMax = 8;
        public string music = "music_forest";
        public string ambience = "amb_forest";
        [Tooltip("Spot the hero appears at when entering without a specific entry.")]
        public string defaultEntry = "spawn";
    }
}
