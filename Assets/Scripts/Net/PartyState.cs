using System;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// This screen's party, as the server last said (<see cref="PartyMsg"/>), and the invitation
    /// waiting for an answer. The party panel and the chat commands read it; nothing here decides.
    /// </summary>
    public static class PartyState
    {
        public static string Leader { get; private set; } = "";
        public static string[] Names { get; private set; } = new string[0];
        public static int[] Heroes { get; private set; } = new int[0];

        /// <summary>Who asked this player into their party, while the invitation lasts; null when nobody.</summary>
        public static string InvitedBy => invitedBy != null && Time.unscaledTime <= inviteUntil ? invitedBy : null;

        public static bool InParty => Names.Length > 0;

        /// <summary>The party changed (members, leader, or none any more).</summary>
        public static event Action Changed;
        /// <summary>An invitation arrived (its sender).</summary>
        public static event Action<string> InviteArrived;

        static string invitedBy;
        static float inviteUntil;

        public static void Apply(PartyMsg m)
        {
            bool was = InParty;
            Leader = m.leader ?? "";
            Names = m.names ?? new string[0];
            Heroes = m.heroes ?? new int[0];
            if (InParty) ClearInvite();
            if (was || InParty) Changed?.Invoke();
        }

        public static void Invited(string from)
        {
            invitedBy = from;
            inviteUntil = Time.unscaledTime + Parties.InviteSeconds;
            GameEvents.RaiseLog($"{from} mời bạn vào tổ đội. Bấm Y để vào, N để từ chối (hoặc /dongy, /tuchoi).", Palette.LogQuest);
            if (GameSession.HasScreen) AudioManager.Play("sfx_ui_click", 0.8f);
            InviteArrived?.Invoke(from);
        }

        /// <summary>Answers the waiting invitation; false when there is none.</summary>
        public static bool Answer(bool yes)
        {
            string from = InvitedBy;
            ClearInvite();
            if (from == null) return false;
            OnlineSession.Ask(new ActRequest { kind = ActKind.PartyAnswer, text = from, value = yes ? 1 : 0 });
            if (!yes) GameEvents.RaiseLog($"Đã từ chối lời mời của {from}.", Palette.LogInfo);
            return true;
        }

        static void ClearInvite()
        {
            bool had = invitedBy != null;
            invitedBy = null;
            if (had) InviteArrived?.Invoke(null);
        }

        /// <summary>The hero of member <paramref name="i"/> on this screen, or null (not in the world yet).</summary>
        public static PlayerController HeroOf(int i) => i >= 0 && i < Heroes.Length && Heroes[i] > 0 ? NetWorld.FindHero(Heroes[i]) : null;

        public static bool IsLeader(string name) => !string.IsNullOrEmpty(name) && string.Equals(LoginCrypto.NormalizeName(name), LoginCrypto.NormalizeName(Leader), StringComparison.OrdinalIgnoreCase);

        /// <summary>Forgets the party (the session ended).</summary>
        public static void Reset()
        {
            Apply(new PartyMsg { leader = "", names = new string[0], heroes = new int[0] });
            ClearInvite();
        }
    }
}
