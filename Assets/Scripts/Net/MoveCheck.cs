using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Chống gian lận di chuyển (Docs/KeHoach-Online.md §4, phase 5). A player's machine moves its
    /// own hero, so the server checks what it hears could have happened: over the last
    /// <see cref="Window"/> seconds the hero may cover twice its walking speed (the network bunches
    /// updates up after a hiccup), plus <see cref="Slack"/>, plus what its dashes and the hits that
    /// pushed it allow. A move beyond that is put back where the hero stood before it. Only
    /// blatant cheats (teleporting, running several times too fast) are caught: a PvE world does not
    /// need more, and a laggy player must never be put back. A move over the limit waits one more
    /// check first, so a dash whose request arrived after its steps still counts.
    /// </summary>
    public class MoveCheck
    {
        public const float Window = 1f;
        /// <summary>Times the walking speed a hero may seem to move at over the window.</summary>
        public const float Tolerance = 2f;
        public const float Slack = 3f;
        /// <summary>Distance a movement skill may add (Lướt 4.2, Ấn Lướt that dash twice).</summary>
        public const float DashAllowance = 9f;

        struct Step
        {
            public float t, length;
        }

        readonly List<Step> steps = new List<Step>();
        readonly List<Step> bonuses = new List<Step>();
        Vector2 last, beforeSuspect;
        bool started, suspect;

        /// <summary>Distance covered over the window at the last check, and what was allowed (for the log).</summary>
        public float Moved { get; private set; }
        public float Allowed { get; private set; }

        /// <summary>The hero is at <paramref name="at"/> by the server's own doing (arrived, fell and got up, a GM's teleport): start over from there.</summary>
        public void Reset(Vector2 at)
        {
            steps.Clear();
            bonuses.Clear();
            last = at;
            started = true;
            suspect = false;
        }

        /// <summary>The server let the hero use a movement skill.</summary>
        public void Dashed(float now) => bonuses.Add(new Step { t = now, length = DashAllowance });

        /// <summary>A hit pushed the hero (knockback <paramref name="impulse"/>, fading at 18 units/s²).</summary>
        public void Pushed(float impulse, float now) => bonuses.Add(new Step { t = now, length = impulse * impulse / 36f + 0.5f });

        /// <summary>
        /// The hero is now at <paramref name="pos"/> and walks at most <paramref name="speed"/>.
        /// False: the move could not have happened; put it back at <paramref name="back"/>.
        /// </summary>
        public bool Check(Vector2 pos, float speed, float now, out Vector2 back)
        {
            back = last;
            if (!started)
            {
                Reset(pos);
                return true;
            }
            steps.Add(new Step { t = now, length = Vector2.Distance(pos, last) });
            steps.RemoveAll(s => s.t <= now - Window);
            bonuses.RemoveAll(b => b.t <= now - Window);
            float moved = 0f, allowed = speed * Tolerance * Window + Slack;
            foreach (var s in steps) moved += s.length;
            foreach (var b in bonuses) allowed += b.length;
            Moved = moved;
            Allowed = allowed;
            if (moved <= allowed)
            {
                suspect = false;
                last = pos;
                return true;
            }
            if (!suspect)
            {
                // once more before judging: a dash's request may still be on its way
                suspect = true;
                beforeSuspect = last;
                last = pos;
                return true;
            }
            back = beforeSuspect;
            Reset(back);
            return false;
        }
    }
}
