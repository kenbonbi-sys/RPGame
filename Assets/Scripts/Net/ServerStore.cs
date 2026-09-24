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

        public byte[] Salt => Convert.FromBase64String(salt ?? "");
        public byte[] Key => Convert.FromBase64String(key ?? "");
    }

    /// <summary>
    /// What a game server keeps on disk (online phase 2, Docs/KeHoach-Online.md), in one folder:
    ///   accounts/&lt;key&gt;.json    name, salt and login key of each account
    ///   characters/&lt;key&gt;.json  each character, in the save file format (one section per system)
    ///   backups/yyyy-MM-dd/      a copy of both, once a day, the last <see cref="BackupsKept"/> kept
    ///   gm.txt                   names of the game masters, one per line
    /// Files are written safely (<see cref="SafeFile"/>): a crash never leaves half a character.
    /// </summary>
    public class ServerStore
    {
        public const int BackupsKept = 14;

        public string Root { get; }
        public string AccountsDir => Path.Combine(Root, "accounts");
        public string CharactersDir => Path.Combine(Root, "characters");
        public string BackupsDir => Path.Combine(Root, "backups");
        public string GmFile => Path.Combine(Root, "gm.txt");

        public ServerStore(string root)
        {
            Root = root;
            Directory.CreateDirectory(AccountsDir);
            Directory.CreateDirectory(CharactersDir);
            Directory.CreateDirectory(BackupsDir);
            if (!File.Exists(GmFile))
                File.WriteAllText(GmFile, "# Tên các tài khoản quản trị (GM), mỗi dòng một tên. GM dùng được bảng lệnh ` trong game.\n");
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
