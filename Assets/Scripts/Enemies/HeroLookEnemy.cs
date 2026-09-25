using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// An enemy or a boss drawn like a hero (<see cref="HeroArt"/>): the Hắc Phong bandits are
    /// people, dressed by a <see cref="HeroLook"/> (a hooded rogue with a bow, a scimitar, a red
    /// sash for their leader). The prefab keeps an animation set of its own with the enemy's clip
    /// names (idle, move, windup, attack, …) in a fixed order; the server plays those, and their
    /// numbers are what goes over the network. A screen swaps in the drawn frames, clip for clip in
    /// the same order (<see cref="Map"/>: which hero pose each enemy clip shows), so a copy online
    /// plays the server's clip numbers right.
    /// </summary>
    public class HeroLookEnemy : MonoBehaviour
    {
        public HeroLook look = new HeroLook();
        [Tooltip("The animator to dress (the body's).")]
        public SpriteAnimator anim;

        /// <summary>The hero pose (side view: bodies flip to face left) that each enemy clip shows.</summary>
        public static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            { "idle", "idle_side" }, { "move", "walk_side" }, { "walk", "walk_side" }, { "windup", "cast_side" },
            { "aim", "cast_side" }, { "attack", "attack_side" }, { "slash", "attack_side" }, { "dash", "dash_side" },
            { "roar", "cast_side" }, { "hurt", "hurt_side" }, { "dead", "dead" },
        };

        /// <summary>The hero pose for an enemy clip (the idle pose for one it does not know).</summary>
        public static string PoseFor(string clip) => Map.TryGetValue(clip, out var pose) ? pose : "idle_side";

        static readonly Dictionary<string, SpriteAnimSet> Dressed = new Dictionary<string, SpriteAnimSet>();

        void Awake()
        {
            if (anim == null) anim = GetComponentInChildren<SpriteAnimator>();
            Dress();
        }

        /// <summary>Swaps the frames of every clip for the look's (a screen only; the server draws nothing).</summary>
        public void Dress()
        {
            if (!GameSession.HasScreen || anim == null || anim.set == null || look == null || !look.HasClass) return;
            var set = DressedSet(anim.set, look);
            if (set == null || set == anim.set) return;
            string clip = anim.Current;
            anim.set = set;
            anim.Play(!string.IsNullOrEmpty(clip) && set.Has(clip) ? clip : "idle", true);
        }

        /// <summary><paramref name="own"/>'s clips, in its order and at its speeds, with <paramref name="look"/>'s frames (cached).</summary>
        static SpriteAnimSet DressedSet(SpriteAnimSet own, HeroLook look)
        {
            string key = own.name + "|" + look.ToJson();
            if (Dressed.TryGetValue(key, out var cached) && cached != null) return cached;
            var drawn = HeroArt.SetFor(look);
            if (drawn == null) return null;
            var set = ScriptableObject.CreateInstance<SpriteAnimSet>();
            set.name = own.name + "_dressed";
            set.hideFlags = HideFlags.DontSave;
            foreach (var c in own.clips)
            {
                if (c == null) continue;
                var pose = drawn.Get(PoseFor(c.name));
                set.clips.Add(new SpriteAnimSet.Clip
                {
                    name = c.name,
                    frames = pose != null && pose.frames != null && pose.frames.Length > 0 ? pose.frames : c.frames,
                    fps = c.fps,
                    loop = c.loop
                });
            }
            Dressed[key] = set;
            return set;
        }
    }
}
