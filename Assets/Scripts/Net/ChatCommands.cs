using System.Globalization;
using System.Text;

namespace RPG
{
    public enum ChatCommandKind
    {
        Say, Party, Whisper, Invite, Leave, Kick, Accept, Decline, AddFriend, RemoveFriend, Friends, Who, Channel, Help, Unknown
    }

    public struct ChatCommand
    {
        public ChatCommandKind kind;
        /// <summary>What follows the command word (a name, a message), trimmed.</summary>
        public string arg;
    }

    /// <summary>
    /// What a line typed in the chat box means (online phase 4, Docs/KeHoach-Online.md): plain text
    /// talks to everyone, "/" starts a command. Command words work with or without Vietnamese
    /// marks (/moi = /mời). <see cref="Help"/> lists them.
    /// </summary>
    public static class ChatCommands
    {
        public const string Help =
            "Lệnh chat: /n <lời> nói với tổ đội · /w <tên> <lời> nhắn riêng · /moi <tên> mời vào tổ đội · " +
            "/dongy, /tuchoi trả lời lời mời · /roi rời tổ đội · /duoi <tên> mời ra (đội trưởng) · " +
            "/ketban <tên>, /huyban <tên>, /banbe bạn bè · /ai ai đang chơi · /kenh kênh.";

        public static ChatCommand Parse(string line)
        {
            line = (line ?? "").Trim();
            if (!line.StartsWith("/")) return new ChatCommand { kind = ChatCommandKind.Say, arg = line };
            int space = line.IndexOf(' ');
            string word = Plain(space < 0 ? line.Substring(1) : line.Substring(1, space - 1));
            string arg = space < 0 ? "" : line.Substring(space + 1).Trim();
            ChatCommandKind kind;
            switch (word)
            {
                case "n": case "p": case "nhom": case "party": case "td": kind = ChatCommandKind.Party; break;
                case "w": case "t": case "nhan": case "whisper": kind = ChatCommandKind.Whisper; break;
                case "moi": case "invite": kind = ChatCommandKind.Invite; break;
                case "roi": case "leave": kind = ChatCommandKind.Leave; break;
                case "duoi": case "kick": kind = ChatCommandKind.Kick; break;
                case "dongy": case "y": case "accept": kind = ChatCommandKind.Accept; break;
                case "tuchoi": case "decline": kind = ChatCommandKind.Decline; break;
                case "ketban": case "friend": kind = ChatCommandKind.AddFriend; break;
                case "huyban": case "unfriend": kind = ChatCommandKind.RemoveFriend; break;
                case "banbe": case "friends": kind = ChatCommandKind.Friends; break;
                case "ai": case "who": case "players": kind = ChatCommandKind.Who; break;
                case "kenh": case "channel": case "k": kind = ChatCommandKind.Channel; break;
                case "giup": case "help": case "?": kind = ChatCommandKind.Help; break;
                default: kind = ChatCommandKind.Unknown; arg = word; break;
            }
            return new ChatCommand { kind = kind, arg = arg };
        }

        /// <summary>Does what the line says (a player's machine, online).</summary>
        public static void Run(string line)
        {
            var c = Parse(line);
            bool needsName = c.kind == ChatCommandKind.Invite || c.kind == ChatCommandKind.Kick ||
                             c.kind == ChatCommandKind.AddFriend || c.kind == ChatCommandKind.RemoveFriend || c.kind == ChatCommandKind.Whisper;
            if (needsName && c.arg.Length == 0)
            {
                Log("Lệnh này cần một tên. " + Help);
                return;
            }
            switch (c.kind)
            {
                case ChatCommandKind.Say: OnlineSession.Say(c.arg); break;
                case ChatCommandKind.Party: OnlineSession.Say(c.arg, ChatChannel.Party); break;
                case ChatCommandKind.Whisper: OnlineSession.Say(c.arg, ChatChannel.Whisper); break;
                case ChatCommandKind.Invite: OnlineSession.Ask(new ActRequest { kind = ActKind.PartyInvite, text = c.arg }); break;
                case ChatCommandKind.Leave: OnlineSession.Ask(new ActRequest { kind = ActKind.PartyLeave }); break;
                case ChatCommandKind.Kick: OnlineSession.Ask(new ActRequest { kind = ActKind.PartyKick, text = c.arg }); break;
                case ChatCommandKind.Accept:
                    if (!PartyState.Answer(true)) Log("Không có lời mời nào đang chờ.");
                    break;
                case ChatCommandKind.Decline:
                    if (!PartyState.Answer(false)) Log("Không có lời mời nào đang chờ.");
                    break;
                case ChatCommandKind.AddFriend: OnlineSession.Ask(new ActRequest { kind = ActKind.Friend, text = c.arg, value = 1 }); break;
                case ChatCommandKind.RemoveFriend: OnlineSession.Ask(new ActRequest { kind = ActKind.Friend, text = c.arg, value = 0 }); break;
                case ChatCommandKind.Friends: OnlineSession.Ask(new ActRequest { kind = ActKind.FriendList }); break;
                case ChatCommandKind.Who: Log(OnlineSession.PlayerList()); break;
                case ChatCommandKind.Channel: OnlineSession.ChannelCommand(c.arg); break;
                case ChatCommandKind.Help: Log(Help); break;
                default: Log($"Không có lệnh /{c.arg}. " + Help); break;
            }
        }

        static void Log(string text) => GameEvents.RaiseLog(text, Palette.LogInfo);

        /// <summary>A command word without case or Vietnamese marks: "Mời" → "moi", "Đồng ý" → "dongy".</summary>
        public static string Plain(string word)
        {
            var sb = new StringBuilder(word.Length);
            foreach (char ch in word.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark || ch == ' ') continue;
                sb.Append(ch == 'đ' ? 'd' : ch);
            }
            return sb.ToString();
        }
    }
}
