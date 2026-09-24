using System.Collections.Generic;

namespace RPG
{
    /// <summary>
    /// Tổ đội (online phase 4, Docs/KeHoach-Online.md): who plays together. Up to
    /// <see cref="MaxMembers"/> players; the one who invited first leads, and when the leader goes
    /// the next member does. A party lasts while its members play (it is not saved). Members near a
    /// kill share it (<see cref="ServerPlayers.ShareKill"/>), talk on their own channel and see each
    /// other's health. The rules only: <see cref="ServerPlayers"/> tells the players.
    /// Names are compared without case (<see cref="LoginCrypto.NameKey"/>).
    /// </summary>
    public class Parties
    {
        public const int MaxMembers = 5;
        /// <summary>Seconds an invitation waits for its answer.</summary>
        public const float InviteSeconds = 60f;

        public class Party
        {
            public string leader;
            public readonly List<string> members = new List<string>();
        }

        class Invitation
        {
            public string from;
            public float until;
        }

        readonly Dictionary<string, Party> byMember = new Dictionary<string, Party>();
        // invited player → who asked them (one invitation at a time, the newest wins)
        readonly Dictionary<string, Invitation> invites = new Dictionary<string, Invitation>();

        static string Key(string name) => LoginCrypto.NameKey(name ?? "");

        /// <summary>The party of <paramref name="name"/>, or null.</summary>
        public Party Of(string name) => name != null && byMember.TryGetValue(Key(name), out var p) ? p : null;

        public bool SameParty(string a, string b)
        {
            var p = Of(a);
            return p != null && p == Of(b);
        }

        /// <summary>Who invited <paramref name="name"/> and is still waiting, or null.</summary>
        public string InvitedBy(string name, float now)
        {
            if (!invites.TryGetValue(Key(name), out var i)) return null;
            if (now <= i.until) return i.from;
            invites.Remove(Key(name));
            return null;
        }

        /// <summary><paramref name="from"/> asks <paramref name="to"/> in. Null when the invitation went out, else why not.</summary>
        public string Invite(string from, string to, float now)
        {
            if (Key(from) == Key(to)) return "Không thể tự mời mình.";
            var mine = Of(from);
            if (mine != null && Key(mine.leader) != Key(from)) return $"Chỉ đội trưởng ({mine.leader}) mới mời được người vào tổ đội.";
            if (mine != null && mine.members.Count >= MaxMembers) return $"Tổ đội đã đủ {MaxMembers} người.";
            var theirs = Of(to);
            if (theirs != null) return theirs == mine ? $"{to} đã ở trong tổ đội." : $"{to} đang ở tổ đội khác.";
            invites[Key(to)] = new Invitation { from = from, until = now + InviteSeconds };
            return null;
        }

        /// <summary>
        /// <paramref name="name"/> answers <paramref name="inviter"/>. Yes and still possible: they
        /// join (a party is made when the inviter had none) and the party is returned; otherwise
        /// null, with <paramref name="why"/> when a yes could not be kept.
        /// </summary>
        public Party Answer(string name, string inviter, bool yes, float now, out string why)
        {
            why = null;
            string asked = InvitedBy(name, now);
            if (asked == null || Key(asked) != Key(inviter))
            {
                why = "Lời mời đã hết hạn.";
                return null;
            }
            invites.Remove(Key(name));
            if (!yes) return null;
            if (Of(name) != null)
            {
                why = "Bạn đã ở trong một tổ đội.";
                return null;
            }
            var p = Of(inviter);
            if (p == null)
            {
                p = new Party { leader = inviter };
                p.members.Add(inviter);
                byMember[Key(inviter)] = p;
            }
            else if (Key(p.leader) != Key(inviter))
            {
                why = $"{inviter} không còn là đội trưởng.";
                return null;
            }
            if (p.members.Count >= MaxMembers)
            {
                why = $"Tổ đội đã đủ {MaxMembers} người.";
                return null;
            }
            p.members.Add(name);
            byMember[Key(name)] = p;
            return p;
        }

        /// <summary>
        /// <paramref name="name"/> leaves (or left the world). Returns the party they left (with
        /// who is still in it: a party of one is over and has no members), or null when they had none.
        /// </summary>
        public Party Leave(string name)
        {
            invites.Remove(Key(name));
            var p = Of(name);
            if (p == null) return null;
            p.members.RemoveAll(m => Key(m) == Key(name));
            byMember.Remove(Key(name));
            if (p.members.Count <= 1)
            {
                foreach (var m in p.members) byMember.Remove(Key(m));
                p.members.Clear();
                return p;
            }
            if (Key(p.leader) == Key(name)) p.leader = p.members[0];
            return p;
        }

        /// <summary>The leader <paramref name="leader"/> sends <paramref name="name"/> out. Null when done, else why not.</summary>
        public string Kick(string leader, string name, out Party party)
        {
            party = Of(leader);
            if (party == null || Key(party.leader) != Key(leader)) return "Chỉ đội trưởng mới mời người ra được.";
            if (Key(leader) == Key(name)) return "Muốn rời tổ đội thì gõ /roi.";
            if (Of(name) != party) return $"{name} không ở trong tổ đội.";
            Leave(name);
            return null;
        }
    }
}
