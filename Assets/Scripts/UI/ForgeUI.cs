using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Lò Rèn (the smith's window, <see cref="Forge"/>): the hero holding their weapon, its
    /// attack, the next tempering with what it costs and what the hero has of it, the metals
    /// (locked until the weapon is tempered enough) and the other weapons of the class.
    /// Closes when the hero walks away from the smith.
    /// </summary>
    public class ForgeUI : UIPanel
    {
        public TMP_FontAsset font;
        public Material fontOutline;
        public Sprite windowSprite, buttonSprite, whiteSprite;

        public static ForgeUI I { get; private set; }

        UiKit kit;
        RectTransform body;
        Image hero;
        TextMeshProUGUI weaponText, costText;
        float animT;
        bool built;

        protected override void Awake()
        {
            base.Awake();
            I = this;
            blocksGameplay = true;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public void Open()
        {
            var p = Players.Local;
            if (p == null || p.stats == null) return;
            if (!p.stats.HasClass)
            {
                Notify.WorldText(p, "Hãy chọn lớp nhân vật trước (bảng Nhân Vật, C).", p.health.HeadPosition + Vector3.up * 0.4f, UiKit.Bad);
                return;
            }
            if (!built) Build();
            Show();
            watched = p;
            p.stats.LookChanged += Refresh;
            if (p.inventory != null) p.inventory.Changed += Refresh;
            Refresh();
        }

        PlayerController watched;

        public override void Close()
        {
            if (watched != null)
            {
                watched.stats.LookChanged -= Refresh;
                if (watched.inventory != null) watched.inventory.Changed -= Refresh;
                watched = null;
            }
            base.Close();
        }

        void Build()
        {
            built = true;
            kit = new UiKit(font, fontOutline, windowSprite, buttonSprite, whiteSprite);
            var root = (RectTransform)window;
            var dim = UiKit.Stretch(root, "Dim");
            UiKit.Img(dim, whiteSprite, new Color(0, 0, 0, 0.55f)).raycastTarget = true;
            body = UiKit.Rect(root, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060, 720));
            kit.Frame(body);
            var t = kit.Txt(body, "Title", "Lò Rèn", 40, UiKit.Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(900, 50));
            t.fontStyle = FontStyles.Bold;
            kit.Txt(body, "Sub", "Thợ Rèn làng Lá Xanh: nâng cấp, đổi kim loại, đổi vũ khí.", 18, UiKit.Muted, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(900, 26), false);
            hero = UiKit.Img(UiKit.Rect(body, "Hero", new Vector2(0, 1), new Vector2(190, -290), new Vector2(256, 256)), null, Color.white);
            hero.preserveAspect = true;
            weaponText = kit.Txt(body, "Weapon", "", 22, UiKit.Cream, TextAlignmentOptions.Top, new Vector2(0, 1), new Vector2(190, -500), new Vector2(330, 120), false);
            costText = kit.Txt(body, "Cost", "", 20, UiKit.Cream, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(640, -210), new Vector2(560, 200), false);
            kit.Btn(body, "Close", "Đóng", new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(200, 50), 22, Close);
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            var p = Players.Local;
            if (p == null || Forge.SmithNear(p) == null)
            {
                Close();
                return;
            }
            // the hero's attack pose, again and again
            var set = p.anim != null ? p.anim.set : null;
            var clip = set != null ? set.Get("attack_down") : null;
            if (clip != null && clip.frames.Length > 0)
            {
                animT += Time.unscaledDeltaTime;
                int f = Mathf.FloorToInt(animT * 4f) % (clip.frames.Length + 3);
                hero.sprite = f < clip.frames.Length ? clip.frames[f] : set.Get("idle_down").frames[0];
            }
        }

        void Refresh()
        {
            var p = Players.Local;
            if (p == null || body == null) return;
            var s = p.stats;
            var db = GameManager.I.db;
            var w = s.Weapon;
            int lv = s.look.upgrade;
            float atk = s.Stats.Get(w.focus ? StatId.MagicAttack : StatId.PhysicalAttack);
            weaponText.text = $"<size=28><b>{w.name} +{lv}</b></size>\n" +
                              $"Kim loại: {HeroLook.Metals[Mathf.Clamp(s.look.metal, 0, HeroLook.Metals.Length - 1)].name}\n" +
                              $"Công: <b>{atk:0.#}</b>" + (lv >= 7 ? "\n<color=#ffe07a>Vũ khí phát sáng</color>" : "");
            // the next tempering
            var sb = new StringBuilder();
            if (lv >= WeaponKinds.MaxUpgrade) sb.Append("<color=#ffe07a>Vũ khí đã được rèn tới +10.</color>\n");
            else
            {
                var cost = Forge.UpgradeCost(lv + 1);
                sb.Append($"<color=#ffe07a>Nâng lên +{lv + 1}</color>  (công +8% mỗi cấp)\n");
                sb.Append(Line("Vàng", p.inventory.gold, cost.gold));
                foreach (var (id, n) in cost.items)
                {
                    var item = db.Item(id);
                    sb.Append(Line(item != null ? item.displayName : id, item != null ? p.inventory.Count(item) : 0, n));
                }
            }
            costText.text = sb.ToString();
            // buttons are rebuilt each time
            var old = body.Find("Actions");
            if (old != null) Destroy(old.gameObject);
            var actions = UiKit.Rect(body, "Actions", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060, 720));
            if (lv < WeaponKinds.MaxUpgrade)
                kit.Btn(actions, "Upgrade", "Rèn", new Vector2(0, 1), new Vector2(640, -330), new Vector2(240, 56), 26, () => Do(Forge.Action.Upgrade)).
                    GetComponentInChildren<TextMeshProUGUI>().color = UiKit.Gold;
            kit.Txt(actions, "MetalLabel", "Kim loại", 20, UiKit.Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0, 1), new Vector2(460, -400), new Vector2(200, 30), false);
            for (int m = 0; m < HeroLook.Metals.Length; m++)
            {
                int k = m;
                var cost = Forge.MetalCost(m);
                bool locked = lv < cost.level;
                var b = kit.Btn(actions, "Metal" + m, HeroLook.Metals[m].name + (locked ? $" (+{cost.level})" : cost.gold > 0 ? $" ({cost.gold}v)" : ""),
                    new Vector2(0, 1), new Vector2(440 + (m % 4) * 150 + 20, -445 - (m / 4) * 50), new Vector2(144, 42), 15, () => Do(Forge.Action.Metal, k));
                b.interactable = !locked && m != s.look.metal;
                var sw = UiKit.Rect(b.transform, "Swatch", new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(14, 14));
                UiKit.Img(sw, whiteSprite, HeroLook.Metals[m].color);
                if (m == s.look.metal) b.GetComponentInChildren<TextMeshProUGUI>().color = UiKit.Gold;
            }
            kit.Txt(actions, "WeaponLabel", "Vũ khí", 20, UiKit.Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0, 1), new Vector2(460, -560), new Vector2(200, 30), false);
            var weapons = s.Class.weapons;
            for (int i = 0; i < weapons.Length; i++)
            {
                string id = weapons[i];
                var wk = WeaponKinds.Get(id);
                var b = kit.Btn(actions, "Weapon" + i, wk != null ? wk.name : id, new Vector2(0, 1), new Vector2(460 + (i % 4) * 150, -605 - (i / 4) * 50),
                    new Vector2(144, 42), 16, () => Do(Forge.Action.Weapon, 0, id));
                b.interactable = id != w.id;
                if (id == w.id) b.GetComponentInChildren<TextMeshProUGUI>().color = UiKit.Gold;
            }
        }

        static string Line(string what, int have, int need) =>
            $"   {what}: <color={(have >= need ? "#8cf08c" : "#ff8c73")}>{have}/{need}</color>\n";

        void Do(Forge.Action what, int value = 0, string text = null)
        {
            Forge.Ask(what, value, text);   // the bag and the look tell when it is done (online: with the server's sheet)
        }
    }
}
