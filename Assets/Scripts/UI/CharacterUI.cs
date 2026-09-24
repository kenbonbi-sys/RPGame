using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>Character sheet (C) of the hero on this screen: level, XP, the four attributes with + buttons and the derived stats.</summary>
    public class CharacterUI : UIPanel
    {
        public TextMeshProUGUI header;
        public TextMeshProUGUI[] values = new TextMeshProUGUI[4];
        public Button[] plusButtons = new Button[4];
        public TextMeshProUGUI pointsText;
        public TextMeshProUGUI derivedText;

        PlayerStats bound;

        static PlayerStats LocalStats => Players.Local != null ? Players.Local.stats : null;

        void Start()
        {
            for (int i = 0; i < plusButtons.Length; i++)
            {
                int k = i;
                if (plusButtons[i] != null) plusButtons[i].onClick.AddListener(() => { if (LocalStats != null) LocalStats.Spend((CoreStat)k); });
            }
        }

        protected override void OnShow() => Refresh();

        protected override void Update()
        {
            base.Update();
            var stats = LocalStats;
            if (bound != stats)
            {
                if (bound != null) bound.Changed -= Refresh;
                bound = stats;
                if (bound != null) bound.Changed += Refresh;
            }
        }

        void OnDestroy()
        {
            if (bound != null) bound.Changed -= Refresh;
        }

        void Refresh()
        {
            var s = LocalStats;
            if (s == null || !IsOpen) return;
            var c = s.Config;
            if (header != null)
                header.text = s.IsMaxLevel ? $"Cấp {s.level}  ·  Tối đa" : $"Cấp {s.level}  ·  {s.xp} / {s.XpToNext} XP";
            for (int i = 0; i < 4; i++)
            {
                var a = (CoreStat)i;
                if (values[i] != null)
                {
                    int spent = s.Allocated(a);
                    values[i].text = spent > 0 ? $"{s.Attribute(a)} <color=#9a93a8>(+{spent})</color>" : s.Attribute(a).ToString();
                }
                if (plusButtons[i] != null) plusButtons[i].gameObject.SetActive(s.statPoints > 0);
            }
            if (pointsText != null)
                pointsText.text = $"Điểm chỉ số: <color=#ffe07a>{s.statPoints}</color>    " +
                                  $"Thiên phú: {s.talentPoints}    Kỹ năng: {s.skillPoints}";
            if (derivedText != null)
            {
                var st = s.Stats;
                float armor = st.Get(StatId.Armor);
                var sb = new StringBuilder();
                sb.Append($"Máu tối đa  <b>{Mathf.Round(st.Get(StatId.MaxHp))}</b>\n");
                sb.Append($"Năng lượng tối đa  <b>{Mathf.Round(st.Get(StatId.MaxEnergy))}</b>\n");
                sb.Append($"Công vật lý  <b>{st.Get(StatId.PhysicalAttack):0.#}</b>   ·   Công phép  <b>{st.Get(StatId.MagicAttack):0.#}</b>\n");
                sb.Append($"Chí mạng  <b>+{st.Get(StatId.CritChance) * 100f:0.##}%</b>  (×{s.CritMultiplier:0.##})\n");
                sb.Append($"Tốc đánh  <b>+{st.Get(StatId.AttackSpeed) * 100f:0.#}%</b>   ·   Hồi Lướt  <b>-{st.Get(StatId.DashCooldownReduction) * 100f:0.#}%</b>\n");
                sb.Append($"Giáp  <b>{armor:0.#}</b>  (-{c.ArmorReduction(armor, s.level) * 100f:0.#}% sát thương cùng cấp)\n");
                sb.Append($"Kháng hệ  <b>{Mathf.Min(c.resistMax, st.Get(StatId.ElementalResist)) * 100f:0.#}%</b>   ·   Trấn Áp  <b>×{s.PoiseMultiplier:0.##}</b>");
                derivedText.text = sb.ToString();
            }
        }
    }
}
