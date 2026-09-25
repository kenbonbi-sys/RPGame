using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>Damage numbers, heals and status words that pop above characters.</summary>
    public class FloatingTextManager : MonoBehaviour
    {
        public RectTransform layer;
        public FloatingText prefab;
        [Tooltip("Damage numbers created up front (shared Pool).")]
        public int prewarm = 24;

        void Start()
        {
            if (prefab != null && layer != null) Pool.Prewarm(prefab.gameObject, prewarm, layer);
        }

        void OnEnable()
        {
            GameEvents.Damaged += OnDamaged;
            GameEvents.Healed += OnHealed;
            GameEvents.WorldText += OnWorldText;
        }

        void OnDisable()
        {
            GameEvents.Damaged -= OnDamaged;
            GameEvents.Healed -= OnHealed;
            GameEvents.WorldText -= OnWorldText;
        }

        void OnDamaged(Health h, DamageInfo d, float amount)
        {
            if (h == null || !h.showDamageNumbers) return;
            Vector3 p = h.HeadPosition + (Vector3)(Random.insideUnitCircle * 0.25f);
            string s = Mathf.RoundToInt(amount).ToString();
            if (h.team == Team.Player)
                Spawn("-" + s, p, Palette.PlayerHurt, 1.0f, FloatingText.Style.Normal);
            else if (d.crit)
                Spawn(s + "!", p, Palette.Crit, 1.45f, FloatingText.Style.Crit);
            else
            {
                Color c = d.type == DamageType.Physical ? Palette.Damage : Color.Lerp(Palette.ForType(d.type), Color.white, 0.25f);
                Spawn(s, p, c, d.dot || amount < 8 ? 0.8f : 1f, FloatingText.Style.Normal);
            }
        }

        void OnHealed(Health h, float amount)
        {
            if (h == null) return;
            Spawn("+" + Mathf.RoundToInt(amount), h.HeadPosition + Vector3.left * 0.3f, Palette.Heal, 0.95f, FloatingText.Style.Rise);
        }

        // status words that pop together at one spot (a hero and the bison that rammed it, both
        // "Choáng!") stack up instead of printing over each other
        struct Recent
        {
            public Vector2 at;
            public float t;
        }

        readonly List<Recent> recent = new List<Recent>();

        void OnWorldText(string text, Vector3 pos, Color c)
        {
            float now = Time.unscaledTime;
            recent.RemoveAll(r => now - r.t > 0.7f);
            // above every word still showing over the same head
            float y = pos.y;
            bool moved = true;
            for (int guard = 0; moved && guard < 8; guard++)
            {
                moved = false;
                foreach (var r in recent)
                    if (Mathf.Abs(r.at.x - pos.x) < 1.8f && Mathf.Abs(r.at.y - y) < 0.4f)
                    {
                        y = r.at.y + 0.42f;
                        moved = true;
                    }
            }
            var at = new Vector3(pos.x, y, pos.z);
            recent.Add(new Recent { at = at, t = now });
            Spawn(text, at, c, 1.05f, FloatingText.Style.Status);
        }

        public void Spawn(string text, Vector3 world, Color c, float scale, FloatingText.Style style)
        {
            if (prefab == null || layer == null) return;
            var ft = Pool.Get(prefab, layer);
            ft.Play(this, text, world, c, scale, style);
        }

        public void Recycle(FloatingText ft) => Pool.Release(ft.gameObject, true);
    }
}
