using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    [CreateAssetMenu(menuName = "RPG/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        public List<AudioClip> clips = new List<AudioClip>();
        Dictionary<string, AudioClip> map;

        public AudioClip Get(string id)
        {
            if (map == null)
            {
                map = new Dictionary<string, AudioClip>();
                foreach (var c in clips)
                    if (c != null) map[c.name] = c;
            }
            map.TryGetValue(id, out var clip);
            return clip;
        }
    }
}
