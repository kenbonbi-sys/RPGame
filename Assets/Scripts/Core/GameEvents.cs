using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>Who died, for quests, XP and the bestiary.</summary>
    public struct KillInfo
    {
        public string id;
        public string name;
        public int level;
        public EnemyRank rank;
        public Vector3 position;
        /// <summary>
        /// Heroes who share the kill: everyone who hurt the enemy. Empty for a kill made by a
        /// script or a cheat, which counts for every hero.
        /// </summary>
        public List<PlayerController> credited;

        /// <summary>Whether <paramref name="p"/> gets the XP, quest progress and Bách Khoa Trùm entry of this kill.</summary>
        public bool Credits(PlayerController p) => credited == null || credited.Count == 0 || credited.Contains(p);
    }

    public enum BannerKind
    {
        /// <summary>Big centred title (zone names, boss intros).</summary>
        Title,
        /// <summary>Small toast under the tracker (quest updates, level up).</summary>
        Quest,
        /// <summary>Huge golden centre text (boss defeated, quest line done).</summary>
        Victory
    }

    /// <summary>
    /// Global game events. Gameplay publishes here; UI, audio, quests and saves listen, so
    /// gameplay code never talks to the HUD directly.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<Health, DamageInfo, float> Damaged;   // target, info, applied amount
        public static event Action<Health, float> Healed;
        public static event Action<Health> Died;
        public static event Action<string, Color> Log;
        public static event Action<KillInfo> EnemyKilled;
        public static event Action<int> LevelUp;                         // new level
        public static event Action<ItemDef, int> ItemPicked;
        public static event Action QuestChanged;
        public static event Action<string> QuestCompleted;               // quest title
        public static event Action<string> DialogueStarted;              // npc id
        public static event Action<string> DialogueEnded;                // npc id
        public static event Action<string> ZoneEntered;                  // zone display name
        public static event Action<string, Vector3, Color> WorldText;    // free floating text
        public static event Action<BannerKind, string, string, Color> Banner;   // kind, title, subtitle, colour
        public static event Action<Health, string> SkillAnnounced;       // who, text over their head
        public static event Action<Health, string, int> BossEngaged;     // boss, name, level
        public static event Action<float> BossDisengaged;                // hide delay
        public static event Action<float> PlayerDowned;                  // seconds until respawn
        public static event Action PlayerRespawned;

        public static void RaiseDamaged(Health h, DamageInfo d, float applied) => Damaged?.Invoke(h, d, applied);
        public static void RaiseHealed(Health h, float amount) => Healed?.Invoke(h, amount);
        public static void RaiseDied(Health h) => Died?.Invoke(h);
        public static void RaiseLog(string msg, Color c) => Log?.Invoke(msg, c);
        public static void RaiseLog(string msg) => Log?.Invoke(msg, Palette.LogInfo);
        public static void RaiseEnemyKilled(KillInfo k) => EnemyKilled?.Invoke(k);
        public static void RaiseLevelUp(int level) => LevelUp?.Invoke(level);
        public static void RaiseItemPicked(ItemDef item, int n) => ItemPicked?.Invoke(item, n);
        public static void RaiseQuestChanged() => QuestChanged?.Invoke();
        public static void RaiseQuestCompleted(string title) => QuestCompleted?.Invoke(title);
        public static void RaiseDialogueStarted(string npcId) => DialogueStarted?.Invoke(npcId);
        public static void RaiseDialogueEnded(string npcId) => DialogueEnded?.Invoke(npcId);
        public static void RaiseZoneEntered(string zone) => ZoneEntered?.Invoke(zone);
        public static void RaiseWorldText(string text, Vector3 pos, Color c) => WorldText?.Invoke(text, pos, c);
        public static void RaiseBanner(BannerKind kind, string title, string subtitle, Color color) => Banner?.Invoke(kind, title, subtitle, color);
        public static void RaiseBanner(BannerKind kind, string title, string subtitle) => Banner?.Invoke(kind, title, subtitle, Color.white);
        public static void RaiseSkillAnnounced(Health owner, string text) => SkillAnnounced?.Invoke(owner, text);
        public static void RaiseBossEngaged(Health boss, string name, int level) => BossEngaged?.Invoke(boss, name, level);
        public static void RaiseBossDisengaged(float hideDelay = 0f) => BossDisengaged?.Invoke(hideDelay);
        public static void RaisePlayerDowned(float respawnIn) => PlayerDowned?.Invoke(respawnIn);
        public static void RaisePlayerRespawned() => PlayerRespawned?.Invoke();

        /// <summary>Clears all listeners (called when the game scene boots, keeps domain-reload-off safe).</summary>
        public static void Reset()
        {
            Damaged = null; Healed = null; Died = null; Log = null; EnemyKilled = null; LevelUp = null;
            ItemPicked = null; QuestChanged = null; QuestCompleted = null; DialogueStarted = null; DialogueEnded = null; ZoneEntered = null; WorldText = null;
            Banner = null; SkillAnnounced = null; BossEngaged = null; BossDisengaged = null; PlayerDowned = null; PlayerRespawned = null;
        }
    }
}
