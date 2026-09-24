using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RPG
{
    /// <summary>An online account (accounts/&lt;key&gt;.json on the server). One character per account, with the same name.</summary>
    [Serializable]
    public class AccountRecord
    {
        public string name;
        /// <summary>Base64 of the PBKDF2 salt and key (<see cref="LoginCrypto"/>); the password itself is never known here.</summary>
        public string salt;
        public string key;
        public int iterations;
        public string created;
        public string lastLogin;
        public bool banned;
        /// <summary>Names this player keeps as friends (online phase 4).</summary>
        public List<string> friends = new List<string>();

        public byte[] Salt => Convert.FromBase64String(salt ?? "");
        public byte[] Key => Convert.FromBase64String(key ?? "");
    }

    /// <summary>
    /// What a game server keeps on disk (online phase 2, Docs/KeHoach-Online.md), in one folder:
    ///   accounts/&lt;key&gt;.json    name, salt and login key of each account
    ///   characters/&lt;key&gt;.json  each character, in the save file format (one section per system)
    ///   backups/yyyy-MM-dd/      a copy of both, once a day, the last <see cref="BackupsKept"/> kept
    ///   gm.txt                   names of the game masters, one per line
    ///   online/&lt;key&gt;.lock      open while the character plays, on any channel server using this folder
    /// Files are written safely (<see cref="SafeFile"/>): a crash never leaves half a character.
    /// Every channel (online phase 4) is its own server process on the same folder: a character
    /// plays on one of them at a time (<see cref="TryLock"/>).
    /// </summary>
    public class ServerStore
    {
        public const int BackupsKept = 14;

        public string Root { get; }
        public string AccountsDir => Path.Combine(Root, "accounts");
        public string CharactersDir => Path.Combine(Root, "characters");
        public string BackupsDir => Path.Combine(Root, "backups");
        public string OnlineDir => Path.Combine(Root, "online");
        public string GmFile => Path.Combine(Root, "gm.txt");

        public ServerStore(string root)
        {
            Root = root;
            Directory.CreateDirectory(AccountsDir);
            Directory.CreateDirectory(CharactersDir);
            Directory.CreateDirectory(BackupsDir);
            try
            {
                if (!File.Exists(GmFile))
                    File.WriteAllText(GmFile, "# Tên các tài khoản quản trị (GM), mỗi dòng một tên. GM dùng được bảng lệnh ` trong game.\n");
            }
            catch (IOException)
            {
                // another channel starting on the same folder is writing it
            }
        }

        /// <summary>Where the server keeps its data unless -data says otherwise: next to the game, in ServerData.</summary>
        public static string DefaultRoot()
        {
            string exeDir = Path.GetDirectoryName(Application.dataPath);   // the folder of RungThiTham.exe (the project folder in the editor)
            return Path.Combine(exeDir, "ServerData");
        }

        string AccountPath(string name) => Path.Combine(AccountsDir, LoginCrypto.NameKey(name) + ".json");
        string CharacterPath(string name) => Path.Combine(CharactersDir, LoginCrypto.NameKey(name) + ".json");

        // ------------------------------------------------------------------ accounts
        public bool AccountExists(string name) => SafeFile.Exists(AccountPath(name));

        public AccountRecord LoadAccount(string name)
        {
            AccountRecord result = null;
            SafeFile.Read(AccountPath(name), text =>
            {
                try
                {
                    result = JsonUtility.FromJson<AccountRecord>(text);
                    return result != null && !string.IsNullOrEmpty(result.key);
                }
                catch
                {
                    return false;
                }
            });
            return result;
        }

        /// <summary>Creates an account; null when the name is taken.</summary>
        public AccountRecord CreateAccount(string name, byte[] salt, byte[] key, int iterations)
        {
            if (AccountExists(name)) return null;
            var a = new AccountRecord
            {
                name = LoginCrypto.NormalizeName(name),
                salt = Convert.ToBase64String(salt),
                key = Convert.ToBase64String(key),
                iterations = iterations,
                created = DateTime.Now.ToString("o"),
                lastLogin = DateTime.Now.ToString("o")
            };
            SaveAccount(a);
            return a;
        }

        public void SaveAccount(AccountRecord a) => SafeFile.Write(AccountPath(a.name), JsonUtility.ToJson(a, true));

        public int CountAccounts() => Directory.GetFiles(AccountsDir, "*.json").Length;

        /// <summary>Game masters may use the console's cheats online. Read on every call, so editing gm.txt needs no restart.</summary>
        public bool IsGm(string name)
        {
            if (!File.Exists(GmFile)) return false;
            string key = LoginCrypto.NameKey(name);
            foreach (var line in File.ReadAllLines(GmFile))
            {
                string l = line.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                if (LoginCrypto.NameKey(l) == key) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ characters
        public bool CharacterExists(string name) => SafeFile.Exists(CharacterPath(name));

        /// <summary>The character's last save, or null for a new character.</summary>
        public SaveFile LoadCharacter(string name) => SaveManager.ReadFile(CharacterPath(name));

        public void SaveCharacter(string name, SaveFile f)
        {
            f.character = LoginCrypto.NormalizeName(name);
            f.savedAt = DateTime.Now.ToString("o");
            SafeFile.Write(CharacterPath(name), JsonUtility.ToJson(f, true));
        }

        // ------------------------------------------------------------------ who plays where
        string LockPath(string name) => Path.Combine(OnlineDir, LoginCrypto.NameKey(name) + ".lock");

        /// <summary>
        /// Claims <paramref name="name"/> for this server while they play on <paramref name="channel"/>:
        /// null when a server (this one or another channel) has them. Dispose to let go; a server
        /// that stops or crashes lets go by itself (Windows closes the file).
        /// </summary>
        public IDisposable TryLock(string name, int channel)
        {
            try
            {
                Directory.CreateDirectory(OnlineDir);
                var fs = new FileStream(LockPath(name), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                fs.SetLength(0);
                var bytes = System.Text.Encoding.UTF8.GetBytes(channel.ToString());
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush();
                return new OnlineLock(fs, LockPath(name));
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>The channel <paramref name="name"/> plays on right now (any server using this folder), or 0.</summary>
        public int ChannelOf(string name)
        {
            string path = LockPath(name);
            if (!File.Exists(path)) return 0;
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) { }
                return 0;   // nobody holds it: left behind by a server that stopped
            }
            catch (IOException)
            {
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var r = new StreamReader(fs))
                        return int.TryParse(r.ReadToEnd().Trim(), out int ch) && ch > 0 ? ch : 1;
                }
                catch (IOException)
                {
                    return 1;
                }
            }
        }

        sealed class OnlineLock : IDisposable
        {
            FileStream stream;
            readonly string path;

            public OnlineLock(FileStream stream, string path)
            {
                this.stream = stream;
                this.path = path;
            }

            public void Dispose()
            {
                if (stream == null) return;
                stream.Dispose();
                stream = null;
                try { File.Delete(path); }
                catch (IOException) { /* another channel is looking at it: it stays, unheld */ }
                catch (UnauthorizedAccessException) { }
            }
        }

        // ------------------------------------------------------------------ backups
        /// <summary>Today's backup folder, or null when there is none yet.</summary>
        public string TodaysBackup()
        {
            string dir = Path.Combine(BackupsDir, DateTime.Now.ToString("yyyy-MM-dd"));
            return Directory.Exists(dir) ? dir : null;
        }

        /// <summary>Copies accounts and characters into backups/yyyy-MM-dd (once a day) and drops the oldest copies.</summary>
        public string Backup(bool force = false)
        {
            string dir = Path.Combine(BackupsDir, DateTime.Now.ToString("yyyy-MM-dd"));
            if (Directory.Exists(dir) && !force) return dir;
            CopyFolder(AccountsDir, Path.Combine(dir, "accounts"));
            CopyFolder(CharactersDir, Path.Combine(dir, "characters"));
            var all = new List<string>(Directory.GetDirectories(BackupsDir));
            all.Sort(StringComparer.Ordinal);
            for (int i = 0; i < all.Count - BackupsKept; i++)
            {
                try { Directory.Delete(all[i], true); }
                catch (Exception e) { Debug.LogWarning($"[Server] Cannot remove old backup {all[i]}: {e.Message}"); }
            }
            return dir;
        }

        static void CopyFolder(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from, "*.json"))
                File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        }
    }
}
