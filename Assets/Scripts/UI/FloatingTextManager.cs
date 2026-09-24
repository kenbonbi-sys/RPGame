using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>Damage numbers, heals and status words that pop above characters.</summary>
    public class FloatingTextManager : MonoBehaviour
    {
        public RectTransform layer;
        public FloatingText prefab;
        readonly Stack<FloatingText> free = new Stack<FloatingText>();

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
                Spawn(s, p, c, d.burnDps > 0 || amount < 8 ? 0.8f : 1f, FloatingText.Style.Normal);
            }
        }

        void OnHealed(Health h, float amount)
        {
            if (h == null) return;
            Spawn("+" + Mathf.RoundToInt(amount), h.HeadPosition + Vector3.left * 0.3f, Palette.Heal, 0.95f, FloatingText.Style.Rise);
        }

        void OnWorldText(string text, Vector3 pos, Color c)
        {
            Spawn(text, pos, c, 1.05f, FloatingText.Style.Status);
        }

        public void Spawn(string text, Vector3 world, Color c, float scale, FloatingText.Style style)
        {
            if (prefab == null || layer == null) return;
            var ft = free.Count > 0 ? free.Pop() : Instantiate(prefab, layer);
            ft.gameObject.SetActive(true);
            ft.transform.SetAsLastSibling();
            ft.Play(this, text, world, c, scale, style);
        }

        public void Recycle(FloatingText ft)
        {
            ft.gameObject.SetActive(false);
            free.Push(ft);
        }
    }
}
