using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RPG
{
    /// <summary>
    /// Đá Truyền Tống (plan §10, T49): a standing stone a hero wakes by walking up to it. A woken
    /// stone is where the hero gets up after a fall (the last one they touched), and standing at
    /// any woken stone they can travel to any other (F at the stone, or the world map, M). Each
    /// hero keeps their own stones (<see cref="WaystoneLog"/>): the stone glows on a screen once
    /// its hero has woken it. Waking and travelling happen where the world's rules run.
    /// </summary>
    public class Waystone : MonoBehaviour
    {
        public string stoneId = "village";
        public string displayName = "Làng Lá Xanh";
        public SpriteRenderer sprite;
        public Sprite dark, lit;
        public Light2D glow;

        /// <summary>How close a hero wakes it, and may travel from it.</summary>
        public const float TouchRadius = 2.2f;

        public static readonly List<Waystone> All = new List<Waystone>();

        float nextCheck;
        bool shownLit;
        static readonly List<PlayerController> Near = new List<PlayerController>();

        /// <summary>Where a traveller or a fallen hero appears: just in front of the stone.</summary>
        public Vector2 Arrival => (Vector2)transform.position + Vector2.down * 1.1f;

        public static Waystone Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var w in All)
                if (w != null && w.stoneId == id) return w;
            return null;
        }

        /// <summary>The stone a hero is standing at, or null.</summary>
        public static Waystone At(Vector2 pos, float slack = 0.5f)
        {
            Waystone best = null;
            float bestSq = (TouchRadius + slack) * (TouchRadius + slack);
            foreach (var w in All)
            {
                if (w == null) continue;
                float d = ((Vector2)w.transform.position - pos).sqrMagnitude;
                if (d > bestSq) continue;
                bestSq = d;
                best = w;
            }
            return best;
        }

        void OnEnable() => All.Add(this);

        void OnDisable() => All.Remove(this);

        void Start()
        {
            MinimapUI.Register(transform, MinimapUI.MarkerKind.Waystone);
            Show(false, true);
        }

        void Update()
        {
            if (GameSession.HasScreen)
            {
                var me = Players.Local;
                Show(me != null && me.waystones != null && me.waystones.Knows(stoneId), false);
            }
            if (!GameSession.IsAuthority || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            Players.Within(transform.position, TouchRadius, Near);
            foreach (var p in Near)
                if (p.waystones != null) p.waystones.Touch(this);
        }

        void Show(bool on, bool force)
        {
            if (on == shownLit && !force) return;
            shownLit = on;
            if (sprite != null && dark != null && lit != null) sprite.sprite = on ? lit : dark;
            if (glow != null) glow.enabled = on;
        }
    }
}
