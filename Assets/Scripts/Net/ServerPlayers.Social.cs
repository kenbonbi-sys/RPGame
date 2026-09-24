using System;
using System.Collections.Generic;
using System.Text;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The social side of the server's players (online phase 4, Docs/KeHoach-Online.md): chat on
    /// the world, party and whisper channels, parties (<see cref="Parties"/>: invitations, members'
    /// health on their screens, kills shared with the members nearby) and friends (a list kept with
    /// the account, with who of them is playing).
    /// </summary>
    public partial class ServerPlayers
    {
        /// <summary>Party members this close to a kill share it: XP, quest progress and their own loot.</summary>
        public const float ShareRadius = 30f;
        public const int MaxFriends = 50;

        public Parties Parties { get; } = new Parties();

        // ================================================================== chat
        void OnChat(NetworkConnection conn, ChatRequest r, Channel channel)
        {
            var s = SessionOf(conn);
            if (s != null) Say(s, r.channel, r.text);
        }

        /// <summary>A player says something: to everyone, to their party, or to one player (the text starts with their name).</summary>
        public void Say(Session s, ChatChannel channel, string text)
        {
            text = CleanChat(text);
            if (s == null || text.Length == 0) return;
            switch (channel)
            {
                case ChatChannel.Party:
                {
                    var p = Parties.Of(s.Name);
                    if (p == null)
                    {
                        Info(s, "Bạn chưa ở trong tổ đội. Mời người khác: /moi <tên>.");
                        return;
                    }
                    var msg = new ChatMsg { channel = ChatChannel.Party, from = s.Name, text = text };
                    foreach (var m in p.members) SendChat(SessionNamed(m), msg);
                    Debug.Log($"[Chat] (tổ đội) {s.Name}: {text}");
                    return;
                }
                case ChatChannel.Whisper:
                {
                    var to = SessionAtStart(text, out string rest);
                    rest = rest.Trim();
                    if (to == null || rest.Length == 0)
                    {
                        Info(s, to == null ? "Người đó không có trong thế giới. Nhắn riêng: /w <tên> <lời>." : "Nhắn riêng: /w <tên> <lời>.");
                        return;
                    }
                    var msg = new ChatMsg { channel = ChatChannel.Whisper, from = s.Name, to = to.Name, text = rest };
                    SendChat(to, msg);
                    if (to != s) SendChat(s, msg);
                    Debug.Log($"[Chat] {s.Name} → {to.Name}: {rest}");
                    return;
                }
                default:
                {
                    var msg = new ChatMsg { from = s.Name, text = text };
                    SendToAll(msg);
                    if (GameSession.HasScreen) NetWorld.ShowChat(msg);
                    Debug.Log($"[Chat] {s.Name}: {text}");
                    return;
                }
            }
        }

        /// <summary>A chat line as it may be shown: one line, at most 120 characters, no rich-text tags.</summary>
        public static string CleanChat(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace('\n', ' ').Replace('\r', ' ').Replace("<", "‹").Replace(">", "›").Trim();
            return text.Length > 120 ? text.Substring(0, 120) : text;
        }

        /// <summary>A server message for everyone ("X đã vào thế giới.").</summary>
        public static void SystemChat(string text)
        {
            var msg = new ChatMsg { text = text, system = true };
            SendToAll(msg);
            if (GameSession.HasScreen) NetWorld.ShowChat(msg);
        }

        /// <summary>One chat line to one player (the host's own shows it here).</summary>
        void SendChat(Session to, ChatMsg msg)
        {
            if (to == null) return;
            if (to.Local)
            {
                if (GameSession.HasScreen) NetWorld.ShowChat(msg);
                return;
            }
            if (nm != null && to.conn != null && to.conn.IsActive) nm.ServerManager.Broadcast(to.conn, msg);
        }

        /// <summary>A line only <paramref name="s"/> sees: the answer to a social command.</summary>
        void Info(Session s, string text)
        {
            if (s == null) return;
            if (s.Local)
            {
                if (GameSession.HasScreen) GameEvents.RaiseLog(text, Palette.LogInfo);
                return;
            }
            if (nm != null && s.conn != null && s.conn.IsActive) nm.ServerManager.Broadcast(s.conn, new ControlMsg { kind = ControlKind.Info, text = text });
        }

        /// <summary>The player called <paramref name="name"/> (case and spacing do not matter), or null.</summary>
        public Session SessionNamed(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string n = Comparable(name);
            foreach (var s in sessions.Values)
                if (Comparable(s.Name) == n) return s;
            return null;
        }

        /// <summary>The player whose name <paramref name="text"/> starts with (the longest one), and what follows it.</summary>
        Session SessionAtStart(string text, out string rest)
        {
            rest = "";
            string t = Comparable(text);
            Session best = null;
            int bestLength = 0;
            foreach (var s in sessions.Values)
            {
                string n = Comparable(s.Name);
                if (n.Length <= bestLength || !t.StartsWith(n, StringComparison.Ordinal)) continue;
                if (t.Length > n.Length && t[n.Length] != ' ') continue;
                best = s;
                bestLength = n.Length;
            }
            if (best != null) rest = t.Length > bestLength ? LoginCrypto.NormalizeName(text).Substring(bestLength) : "";
            return best;
        }

        static string Comparable(string name) => LoginCrypto.NormalizeName(name).ToLowerInvariant();

        // ================================================================== parties
        void PartyInvite(Session s, string name)
        {
            var to = SessionNamed(name);
            if (to == null)
            {
                Info(s, $"Không thấy \"{LoginCrypto.NormalizeName(name)}\" trong thế giới.");
                return;
            }
            string why = Parties.Invite(s.Name, to.Name, Time.unscaledTime);
            if (why != null)
            {
                Info(s, why);
                return;
            }
            Info(s, $"Đã mời {to.Name} vào tổ đội.");
            if (to.Local)
            {
                if (GameSession.HasScreen) PartyState.Invited(s.Name);
            }
            else if (nm != null) nm.ServerManager.Broadcast(to.conn, new ControlMsg { kind = ControlKind.PartyInvite, text = s.Name });
        }

        void PartyAnswer(Session s, string inviter, bool yes)
        {
            var p = Parties.Answer(s.Name, inviter, yes, Time.unscaledTime, out string why);
            var from = SessionNamed(inviter);
            if (!yes)
            {
                if (why == null && from != null) Info(from, $"{s.Name} từ chối lời mời vào tổ đội.");
                return;
            }
            if (p == null)
            {
                Info(s, why ?? "Không vào được tổ đội.");
                return;
            }
            PartyChanged(p.members, $"{s.Name} đã vào tổ đội.");
        }

        void PartyLeave(Session s, string news = null)
        {
            var p = Parties.Of(s.Name);
            if (p == null)
            {
                Info(s, "Bạn không ở trong tổ đội.");
                return;
            }
            var everyone = new List<string>(p.members);
            Parties.Leave(s.Name);
            PartyChanged(everyone, news ?? $"{s.Name} đã rời tổ đội.");
        }

        void PartyKick(Session s, string name)
        {
            var target = SessionNamed(name);
            var p = Parties.Of(s.Name);
            var everyone = p != null ? new List<string>(p.members) : null;
            string why = Parties.Kick(s.Name, target != null ? target.Name : LoginCrypto.NormalizeName(name), out _);
            if (why != null)
            {
                Info(s, why);
                return;
            }
            PartyChanged(everyone, $"{target?.Name ?? name} đã bị mời ra khỏi tổ đội.");
        }

        /// <summary>After a change: everyone who was or is in the party gets it as it is now, and the news.</summary>
        void PartyChanged(IEnumerable<string> everyone, string news)
        {
            var told = new HashSet<Session>();
            foreach (var name in everyone)
            {
                var s = SessionNamed(name);
                if (s == null || !told.Add(s)) continue;
                SendParty(s);
                if (news != null) SendChat(s, new ChatMsg { channel = ChatChannel.Party, text = news, system = true });
            }
        }

        /// <summary><paramref name="s"/>'s party as it is now (nobody: not in one).</summary>
        void SendParty(Session s)
        {
            var msg = PartyMessage(Parties.Of(s.Name));
            if (s.Local)
            {
                if (GameSession.HasScreen) PartyState.Apply(msg);
                return;
            }
            if (nm != null && s.conn != null && s.conn.IsActive) nm.ServerManager.Broadcast(s.conn, msg);
        }

        PartyMsg PartyMessage(Parties.Party p)
        {
            if (p == null || p.members.Count == 0) return new PartyMsg { leader = "", names = new string[0], heroes = new int[0] };
            var heroes = new int[p.members.Count];
            for (int i = 0; i < heroes.Length; i++)
            {
                var m = SessionNamed(p.members[i]);
                heroes[i] = m != null && m.hero != null ? NetWorld.IdOf(m.hero) : 0;
            }
            return new PartyMsg { leader = p.leader, names = p.members.ToArray(), heroes = heroes };
        }

        /// <summary>
        /// A kill: members of the credited heroes' parties who are on their feet within
        /// <see cref="ShareRadius"/> share it (added to <paramref name="credited"/>). Offline, or
        /// nobody in a party, it changes nothing.
        /// </summary>
        public static void ShareKill(List<PlayerController> credited, Vector2 at)
        {
            var sp = I;
            if (sp == null) return;
            ShareKill(credited, at, hero =>
            {
                string name = NameOf(hero);
                var p = name != null ? sp.Parties.Of(name) : null;
                if (p == null) return null;
                var heroes = new List<PlayerController>(p.members.Count);
                foreach (var m in p.members)
                {
                    var h = sp.SessionNamed(m)?.hero;
                    if (h != null) heroes.Add(h);
                }
                return heroes;
            });
        }

        /// <summary>The rule of <see cref="ShareKill(List{PlayerController}, Vector2)"/>, with the heroes of a hero's party from <paramref name="partyOf"/> (null: no party).</summary>
        public static void ShareKill(List<PlayerController> credited, Vector2 at, Func<PlayerController, IEnumerable<PlayerController>> partyOf)
        {
            if (credited == null || partyOf == null) return;
            int n = credited.Count;   // only the ones who hit share: the members added here do not bring their own party
            for (int i = 0; i < n; i++)
            {
                var party = partyOf(credited[i]);
                if (party == null) continue;
                foreach (var hero in party)
                {
                    if (hero == null || hero.IsDead || credited.Contains(hero)) continue;
                    if (Vector2.Distance(hero.transform.position, at) <= ShareRadius) credited.Add(hero);
                }
            }
        }

        // ================================================================== friends
        void Friend(Session s, string name, bool add)
        {
            string n = LoginCrypto.NormalizeName(name);
            var list = s.account.friends ?? (s.account.friends = new List<string>());
            int at = list.FindIndex(f => Comparable(f) == Comparable(n));
            if (!add)
            {
                if (at < 0)
                {
                    Info(s, $"{n} không có trong danh sách bạn bè.");
                    return;
                }
                list.RemoveAt(at);
                Store.SaveAccount(s.account);
                Info(s, $"Đã bỏ {n} khỏi danh sách bạn bè.");
                return;
            }
            if (Comparable(n) == Comparable(s.Name))
            {
                Info(s, "Không thể kết bạn với chính mình.");
                return;
            }
            if (at >= 0)
            {
                Info(s, $"{list[at]} đã là bạn bè.");
                return;
            }
            var account = Store.LoadAccount(n);
            if (account == null)
            {
                Info(s, $"Chưa có nhân vật tên \"{n}\".");
                return;
            }
            if (list.Count >= MaxFriends)
            {
                Info(s, $"Danh sách bạn bè đã đủ {MaxFriends} người.");
                return;
            }
            list.Add(account.name);
            Store.SaveAccount(s.account);
            Info(s, $"Đã thêm {account.name} vào danh sách bạn bè.");
            var other = SessionNamed(account.name);
            if (other != null && other != s) Info(other, $"{s.Name} đã thêm bạn vào danh sách bạn bè. Gõ /ketban {s.Name} để thêm lại.");
        }

        void FriendList(Session s)
        {
            var list = s.account.friends;
            if (list == null || list.Count == 0)
            {
                Info(s, "Chưa có bạn bè. Thêm bạn: /ketban <tên>.");
                return;
            }
            var sb = new StringBuilder($"Bạn bè ({list.Count}): ");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                bool on = SessionNamed(list[i]) != null;
                sb.Append(on ? $"<color=#7dff9a>{list[i]} (đang chơi)</color>" : $"{list[i]} (vắng)");
            }
            Info(s, sb.ToString());
        }
    }
}
