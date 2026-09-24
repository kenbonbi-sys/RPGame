using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// A named set of frame animations (e.g. "idle_down", "walk_side").
    /// Edit in the Inspector: drag new sprites into a clip to change the art.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Sprite Anim Set")]
    public class SpriteAnimSet : ScriptableObject
    {
        [Serializable]
        public class Clip
        {
            public string name;
            public Sprite[] frames;
            public float fps = 8f;
            public bool loop = true;

            public float Length => frames == null || fps <= 0 ? 0 : frames.Length / fps;
        }

        public List<Clip> clips = new List<Clip>();
        Dictionary<string, Clip> map;

        public Clip Get(string clipName)
        {
            if (map == null || map.Count != clips.Count)
            {
                map = new Dictionary<string, Clip>();
                foreach (var c in clips)
                    if (c != null && !string.IsNullOrEmpty(c.name)) map[c.name] = c;
            }
            map.TryGetValue(clipName, out var clip);
            return clip;
        }

        public bool Has(string clipName) => Get(clipName) != null;

        void OnValidate() => map = null;
    }
}
