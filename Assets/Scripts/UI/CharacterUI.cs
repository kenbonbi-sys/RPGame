using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Character sheet (C) of the hero on this screen, laid out like D&D's: who they are (people,
    /// class, weapon), level and XP, the six ability scores in the sheet's order (STR DEX CON INT
    /// WIS CHA) with their modifiers, a star on the saving throws the class is proficient in, the
    /// + buttons to spend points, and the numbers the scores make.
    /// </summary>
    public class CharacterUI : UIPanel
    {
        public TextMeshProUGUI header;
        public TextMeshProUGUI identity;
        public TextMeshProUGUI[] names = new TextMeshProUGUI[CoreStats.Count];
        public TextMeshProUGUI[] values = new TextMeshProUGUI[CoreStats.Count];
        public Button[] plusButtons = new Button[CoreStats.Count];
        public TextMeshProUGUI pointsText;
        public TextMeshProUGUI derivedText;
        [Tooltip("Opens the character creator to change the looks (people and class stay).")]
        public Button looksButton;

        PlayerStats bound;

        static PlayerStats LocalStats => Players.Local != null ? Players.Local.stats : null;

        void Start()
        {
            for (int i = 0; i < plusButtons.Length; i++)
            {
                var a = CoreStats.SheetOrder[Mathf.Min(i, CoreStats.Count - 1)];
                if (plusButtons[i] != null) plusButtons[i].onClick.AddListener(() => { if (LocalStats != null) LocalStats.Spend(a); });
            }
            if (looksButton != null)
                looksButton.onClick.AddListener(() =>
                {
                    if (CharacterCreatorUI.I == null || LocalStats == null) return;
                    Close();
                    CharacterCreatorUI.I.Open(LocalStats.HasClass ? CharacterCreatorUI.Mode.Looks : CharacterCreatorUI.Mode.New);
                });
        }

        protected override void OnShow() => Refresh();

        protected override void Update()
        {
            base.Update();
            var stats = LocalStats;
            if (bound != stats)
            {
                if (bound != null)
                {
                    bound.Changed -= Refresh;
                    bound.LookChanged -= Refresh;
                }
                bound = stats;
                if (bound != null)
                {
                    bound.Changed += Refresh;
                    bound.LookChanged += Refresh;
                }
            }
        }

        void OnDestroy()
        {
            if (bound == null) return;
            bound.Changed -= Refresh;
            bound.LookChanged -= Refresh;
        }

        void Refresh()
        {
            var s = LocalStats;
            if (s == null || !IsOpen) return;
            var c = s.Config;
            var cls = s.Class;
            var race = s.Race;
            if (header != null)
                header.text = s.IsMaxLevel ? $"Cấp {s.level}  ·  Tối đa" : $"Cấp {s.level}  ·  {s.xp} / {s.XpToNext} XP";
            if (identity != null)
            {
                var w = s.Weapon;
                string weapon = w != null ? w.name + (s.look.upgrade > 0 ? $" +{s.look.upgrade}" : "") : "";
                identity.text = cls == null
                    ? "Chưa chọn lớp nhân vật"
                    : $"{(race != null ? race.displayName : "")} · {cls.displayName} (d{cls.hitDie}) · {weapon}";
            }
            for (int i = 0; i < CoreStats.Count && i < values.Length; i++)
            {
                var a = CoreStats.SheetOrder[i];
                if (values[i] != null)
                {
                    int score = s.Attribute(a);
                    int spent = s.Allocated(a);
                    string star = cls != null && cls.Saves(a) ? "<color=#ffe07a>★</color> " : "";
                    string mod = $"<color=#9a93a8>({CoreStats.ModifierText(score)})</color>";
                    values[i].text = spent > 0 ? $"{star}{score} {mod} <size=16><color=#86c4f5>+{spent}</color></size>" : $"{star}{score} {mod}";
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
                sb.Append($"Máu tối đa  <b>{Mathf.Round(st.Get(StatId.MaxHp))}</b>   ·   Năng lượng  <b>{Mathf.Round(st.Get(StatId.MaxEnergy))}</b>\n");
                sb.Append($"Công vật lý  <b>{st.Get(StatId.PhysicalAttack):0.#}</b>   ·   Công phép  <b>{st.Get(StatId.MagicAttack):0.#}</b>\n");
                sb.Append($"Chí mạng  <b>+{st.Get(StatId.CritChance) * 100f:0.##}%</b>  (×{s.CritMultiplier:0.##})   ·   Tốc đánh  <b>+{st.Get(StatId.AttackSpeed) * 100f:0.#}%</b>\n");
                sb.Append($"Giáp  <b>{armor:0.#}</b>  (-{c.ArmorReduction(armor, s.level) * 100f:0.#}% sát thương cùng cấp)   ·   Kháng hệ  <b>{Mathf.Min(c.resistMax, st.Get(StatId.ElementalResist)) * 100f:0.#}%</b>\n");
                sb.Append($"Giảm hồi chiêu  <b>{st.Get(StatId.CooldownReduction) * 100f:0.#}%</b>   ·   Hồi máu  <b>×{s.HealingPower:0.##}</b>   ·   Vàng  <b>+{s.GoldFind * 100f:0.#}%</b>\n");
                sb.Append($"Hồi Lướt  <b>-{st.Get(StatId.DashCooldownReduction) * 100f:0.#}%</b>   ·   Trấn Áp  <b>×{s.PoiseMultiplier:0.##}</b>");
                if (race != null && !string.IsNullOrEmpty(race.traits))
                    sb.Append("\n\n<color=#ffe07a>").Append(race.displayName).Append("</color>\n<size=15>").Append(race.traits.Replace("\n", " · ")).Append("</size>");
                derivedText.text = sb.ToString();
            }
        }
    }
}
