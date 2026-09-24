using System;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Plays frame animations from a <see cref="SpriteAnimSet"/> on a SpriteRenderer.
    /// Supports 4-directional clips named base_down / base_up / base_side (left = flipped side).
    /// </summary>
    public class SpriteAnimator : MonoBehaviour
    {
        public SpriteAnimSet set;
        public SpriteRenderer target;
        public string startClip = "idle_down";
        public float speed = 1f;
        public bool useUnscaledTime;
        [Tooltip("Hide the renderer when a non-looping clip ends (one-shot VFX flipbooks).")]
        public bool hideWhenDone;

        /// <summary>Raised when a new frame is shown: (clip, frameIndex).</summary>
        public event Action<string, int> FrameChanged;

        SpriteAnimSet.Clip clip;
        string clipName;
        int frame;
        float timer;
        bool finished;
        Action onComplete;
        float holdUntil;

        public string Current => clipName;
        public bool Finished => finished;
        public int Frame => frame;
        /// <summary>The frame is being held (a hit-stop felt by this character only).</summary>
        public bool Held => Time.unscaledTime < holdUntil;

        /// <summary>Holds the current frame for <paramref name="seconds"/> of real time.</summary>
        public void Hold(float seconds) => holdUntil = Mathf.Max(holdUntil, Time.unscaledTime + seconds);

        void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            if (!string.IsNullOrEmpty(startClip) && (clip == null || !Application.isPlaying))
                Play(startClip, true);
            else if (clip != null)
                Play(clipName, true);
        }

        public bool Has(string name) => set != null && set.Has(name);

        public void Play(string name, bool restart = false, Action completed = null)
        {
            if (set == null) return;
            if (!restart && name == clipName && !finished)
            {
                if (completed != null) onComplete = completed;
                return;
            }
            var c = set.Get(name);
            if (c == null || c.frames == null || c.frames.Length == 0) return;
            clip = c;
            clipName = name;
            frame = 0;
            timer = 0;
            finished = false;
            onComplete = completed;
            if (target != null && hideWhenDone) target.enabled = true;
            Apply();
        }

        /// <summary>Plays base_{down|up|side}, flipping the renderer when facing left.</summary>
        public void PlayDir(string baseName, Vector2 facing, bool restart = false, Action completed = null)
        {
            string dir = Util.Dir4(facing, out bool flip);
            string full = baseName + "_" + dir;
            if (!Has(full)) full = baseName;
            if (target != null) target.flipX = dir == "side" && flip;
            Play(full, restart, completed);
        }

        public float LengthOf(string name)
        {
            var c = set != null ? set.Get(name) : null;
            return c != null ? c.Length : 0f;
        }

        void Apply()
        {
            if (target != null && clip != null && frame < clip.frames.Length)
                target.sprite = clip.frames[frame];
            FrameChanged?.Invoke(clipName, frame);
        }

        void Update()
        {
            if (clip == null || finished || Held) return;
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            timer += dt * speed * clip.fps;
            while (timer >= 1f)
            {
                timer -= 1f;
                frame++;
                if (frame >= clip.frames.Length)
                {
                    if (clip.loop)
                    {
                        frame = 0;
                    }
                    else
                    {
                        frame = clip.frames.Length - 1;
                        finished = true;
                        var cb = onComplete;
                        onComplete = null;
                        Apply();
                        if (hideWhenDone && target != null) target.enabled = false;
                        cb?.Invoke();
                        return;
                    }
                }
                Apply();
            }
        }
    }
}
