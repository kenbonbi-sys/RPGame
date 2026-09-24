using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Messages for one hero's player: "Nhận được…", "Lên cấp!", quest banners, their own sounds.
    /// The rules call these with the hero concerned; the message shows on this screen when that
    /// hero is the one played here, and a server sends it to the player of any other hero
    /// (online phase 3). Nobody else sees it.
    /// </summary>
    public static class Notify
    {
        public static void Log(PlayerController hero, string text, Color color) =>
            Send(hero, new NoticeMsg { kind = NoticeKind.Log, text = text, color = color });

        public static void Log(PlayerController hero, string text) => Log(hero, text, Palette.LogInfo);

        public static void Banner(PlayerController hero, BannerKind kind, string title, string subtitle, Color? color = null) =>
            Send(hero, new NoticeMsg { kind = NoticeKind.Banner, style = (byte)kind, text = title, text2 = subtitle, color = color ?? Color.white });

        /// <summary>A flat sound for this player only (a quest fanfare, "no potion").</summary>
        public static void Sound(PlayerController hero, string id, float volume = 1f, float pitchVariance = 0.06f) =>
            Send(hero, new NoticeMsg { kind = NoticeKind.Sound, text = id, a = volume, b = pitchVariance });

        /// <summary>A floating word only this player sees ("Hết bình!", "+12 XP").</summary>
        public static void WorldText(PlayerController hero, string text, Vector3 pos, Color color) =>
            Send(hero, new NoticeMsg { kind = NoticeKind.WorldText, text = text, pos = pos, color = color });

        public static void ItemPicked(PlayerController hero, ItemDef item, int count)
        {
            if (item == null || count <= 0) return;
            Send(hero, new NoticeMsg { kind = NoticeKind.ItemPicked, text = item.id, a = count });
        }

        public static void LevelUp(PlayerController hero, int level) =>
            Send(hero, new NoticeMsg { kind = NoticeKind.LevelUp, a = level });

        public static void QuestCompleted(PlayerController hero, string title) =>
            Send(hero, new NoticeMsg { kind = NoticeKind.QuestCompleted, text = title });

        public static void QuestChanged(PlayerController hero) =>
            Send(hero, new NoticeMsg { kind = NoticeKind.QuestChanged });

        public static void ScreenFlash(PlayerController hero, Color c, float strength, float duration) =>
            Send(hero, new NoticeMsg { kind = NoticeKind.ScreenFlash, color = c, a = strength, b = duration });

        public static void Shake(PlayerController hero, float amount) =>
            Send(hero, new NoticeMsg { kind = NoticeKind.Shake, a = amount });

        /// <summary>Slow motion on this player's screen (offline only: a shared world never slows).</summary>
        public static void SlowMo(PlayerController hero, float scale, float seconds, float easeOut) =>
            Send(hero, new NoticeMsg { kind = NoticeKind.SlowMo, a = scale, b = seconds, pos = new Vector3(easeOut, 0, 0) });

        // ------------------------------------------------------------------ plumbing
        static void Send(PlayerController hero, NoticeMsg m)
        {
            if (hero == null || hero.IsLocal)
            {
                if (GameSession.HasScreen) Show(m);
                return;
            }
            if (GameSession.Serving) ServerPlayers.SendNotice(hero, m);
        }

        /// <summary>Shows a notice on this screen (from the rules here, or from the server).</summary>
        public static void Show(NoticeMsg m)
        {
            switch (m.kind)
            {
                case NoticeKind.Log:
                    GameEvents.RaiseLog(m.text, m.color);
                    break;
                case NoticeKind.Banner:
                    GameEvents.RaiseBanner((BannerKind)m.style, m.text, m.text2, m.color);
                    break;
                case NoticeKind.Sound:
                    AudioManager.Play(m.text, m.a, m.b);
                    break;
                case NoticeKind.WorldText:
                    GameEvents.RaiseWorldText(m.text, m.pos, m.color);
                    break;
                case NoticeKind.ItemPicked:
                {
                    var db = GameManager.I != null ? GameManager.I.db : null;
                    var item = db != null ? db.Item(m.text) : null;
                    if (item != null) GameEvents.RaiseItemPicked(item, Mathf.RoundToInt(m.a));
                    break;
                }
                case NoticeKind.LevelUp:
                    GameEvents.RaiseLevelUp(Mathf.RoundToInt(m.a));
                    break;
                case NoticeKind.QuestCompleted:
                    GameEvents.RaiseQuestCompleted(m.text);
                    break;
                case NoticeKind.QuestChanged:
                    GameEvents.RaiseQuestChanged();
                    break;
                case NoticeKind.ScreenFlash:
                    ScreenFX.Flash(m.color, m.a, m.b);
                    break;
                case NoticeKind.Shake:
                    CameraRig.Shake(m.a);
                    break;
                case NoticeKind.SlowMo:
                    TimeFX.SlowMo(m.a, m.b, m.pos.x);
                    break;
            }
        }
    }
}
