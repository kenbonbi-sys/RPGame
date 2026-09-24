using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>Active buffs (shield, regen, blade storm) with remaining time.</summary>
    public class BuffBarUI : MonoBehaviour
    {
        public RectTransform container;
        public GameObject iconPrefab;   // Image + child TMP "time"
        readonly List<GameObject> pool = new List<GameObject>();

        void Update()
        {
            var p = Players.Local;
            if (p == null || iconPrefab == null) return;
            var buffs = p.buffs;
            while (pool.Count < buffs.Count)
            {
                var go = Instantiate(iconPrefab, container);
                pool.Add(go);
            }
            for (int i = 0; i < pool.Count; i++)
            {
                bool on = i < buffs.Count;
                pool[i].SetActive(on);
                if (!on) continue;
                var b = buffs[i];
                var img = pool[i].GetComponent<Image>();
                if (img != null) img.sprite = b.icon;
                var t = pool[i].GetComponentInChildren<TextMeshProUGUI>();
                if (t != null) t.text = Mathf.CeilToInt(b.Remaining).ToString();
                var fill = pool[i].transform.Find("fill");
                if (fill != null)
                {
                    var fi = fill.GetComponent<Image>();
                    if (fi != null) fi.fillAmount = b.duration > 0 ? 1f - b.Remaining / b.duration : 0;
                }
            }
        }
    }
}
