using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RPG
{
    /// <summary>Left-side message feed ("• Bách Khoa Trùm: ghi lại kỹ năng ...").</summary>
    public class CombatLogUI : MonoBehaviour
    {
        public RectTransform container;
        public LogLineUI linePrefab;
        public int maxLines = 6;
        public float lineLifetime = 9f;

        readonly List<LogLineUI> lines = new List<LogLineUI>();

        void OnEnable()
        {
            GameEvents.Log += Add;
            GameEvents.ItemPicked += OnItem;
        }

        void OnDisable()
        {
            GameEvents.Log -= Add;
            GameEvents.ItemPicked -= OnItem;
        }

        void OnItem(ItemDef item, int n)
        {
            if (item == null) return;
            if (item.kind == ItemKind.Currency)
            {
                // coins are frequent: merge into the last line if it is a gold line
                if (lines.Count > 0 && lines[lines.Count - 1].kind == "gold")
                {
                    var l = lines[lines.Count - 1];
                    l.amount += n * Mathf.Max(1, item.value);
                    l.Set($"Nhận được <color=#ffd84a>{l.amount} vàng</color>", Palette.LogLoot);
                    return;
                }
                var line = AddLine($"Nhận được <color=#ffd84a>{n * Mathf.Max(1, item.value)} vàng</color>", Palette.LogLoot);
                if (line != null)
                {
                    line.kind = "gold";
                    line.amount = n * Mathf.Max(1, item.value);
                }
                return;
            }
            string hex = ColorUtility.ToHtmlStringRGB(item.RarityColor);
            Add($"Nhận được <color=#{hex}>{item.displayName}</color> x{n}", Palette.LogLoot);
        }

        void Add(string msg, Color c) => AddLine(msg, c);

        LogLineUI AddLine(string msg, Color c)
        {
            if (linePrefab == null || container == null) return null;
            var l = Pool.Get(linePrefab, container);
            l.lifetime = lineLifetime;
            l.Set(msg, c);
            lines.Add(l);
            while (lines.Count > maxLines)
            {
                if (lines[0] != null) Pool.Release(lines[0].gameObject, true);
                lines.RemoveAt(0);
            }
            return l;
        }

        void Update()
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i] == null || lines[i].Expired)
                {
                    if (lines[i] != null) Pool.Release(lines[i].gameObject, true);
                    lines.RemoveAt(i);
                }
            }
        }
    }
}
