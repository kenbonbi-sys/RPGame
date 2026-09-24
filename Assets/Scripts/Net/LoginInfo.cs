using System;
using System.IO;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Who this player logs in as (online phase 2). The title screen fills it in; the game can
    /// remember the login on this machine, so next time one click is enough. What it remembers is
    /// the account key made from the password (<see cref="LoginCrypto"/>), never the password.
    /// From the command line: -login &lt;name&gt; &lt;password&gt; (add -register to make the character
    /// when it does not exist yet: automated runs).
    /// </summary>
    public static class LoginInfo
    {
        public static string Name = "";
        /// <summary>Typed on the title screen; empty when logging in with the remembered key.</summary>
        public static string Password = "";
        public static LoginMode Mode = LoginMode.Login;
        public static bool Remember = true;

        /// <summary>The last answer of a server (shown on the title screen after a refused login).</summary>
        public static LoginResult? LastResult;
        /// <summary>Why the last online session ended, for the title screen (null: it did not end badly).</summary>
        public static string LastError;

        /// <summary>Tests point this at a temp folder.</summary>
        public static string FolderOverride;

        static string Folder => !string.IsNullOrEmpty(FolderOverride) ? FolderOverride : Path.Combine(Application.persistentDataPath, "online");
        static string SavedPath => Path.Combine(Folder, "login.json");

        [Serializable]
        class Saved
        {
            public string name;
            public string salt;
            public string key;
        }

        static Saved saved;
        static bool loaded;

        static Saved Remembered
        {
            get
            {
                if (loaded) return saved;
                loaded = true;
                try
                {
                    if (File.Exists(SavedPath)) saved = JsonUtility.FromJson<Saved>(File.ReadAllText(SavedPath));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Login] Cannot read the remembered login: " + e.Message);
                    saved = null;
                }
                return saved;
            }
        }

        /// <summary>Name of the remembered login on this machine, or null.</summary>
        public static string RememberedName => Remembered != null && !string.IsNullOrEmpty(Remembered.key) ? Remembered.name : null;

        /// <summary>Logs in with the remembered key next time (no password typed).</summary>
        public static bool UseRemembered()
        {
            if (RememberedName == null) return false;
            Name = Remembered.name;
            Password = "";
            Mode = LoginMode.Login;
            return true;
        }

        /// <summary>
        /// The account key for the server's salt: from the typed password, or the remembered key
        /// when nothing was typed and it was made with this salt. Null: no way to log in.
        /// </summary>
        public static byte[] KeyFor(byte[] salt, int iterations)
        {
            if (!string.IsNullOrEmpty(Password)) return LoginCrypto.DeriveKey(Password, salt, iterations);
            var r = Remembered;
            if (r == null || string.IsNullOrEmpty(r.key) || LoginCrypto.NameKey(r.name) != LoginCrypto.NameKey(Name)) return null;
            if (r.salt != Convert.ToBase64String(salt)) return null;
            return Convert.FromBase64String(r.key);
        }

        /// <summary>The server accepted this login: remember it (when asked to) and forget the typed password.</summary>
        public static void Accepted(string name, byte[] salt, byte[] key)
        {
            Name = name;
            Password = "";
            Mode = LoginMode.Login;
            if (!Remember || key == null) return;
            saved = new Saved { name = name, salt = Convert.ToBase64String(salt), key = Convert.ToBase64String(key) };
            loaded = true;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(SavedPath, JsonUtility.ToJson(saved));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Login] Cannot remember the login: " + e.Message);
            }
        }

        /// <summary>Logs out on this machine: the next login asks for the password again.</summary>
        public static void Forget()
        {
            saved = null;
            loaded = true;
            Password = "";
            try
            {
                if (File.Exists(SavedPath)) File.Delete(SavedPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Login] Cannot forget the login: " + e.Message);
            }
        }

        /// <summary>-login &lt;name&gt; &lt;password&gt; [-register]: a login from the command line (automated runs, a GM's shortcut).</summary>
        public static bool ReadCommandLine(string[] args)
        {
            int i = Array.IndexOf(args, "-login");
            if (i < 0 || i + 2 >= args.Length) return false;
            Name = args[i + 1];
            Password = args[i + 2];
            Mode = Array.IndexOf(args, "-register") >= 0 ? LoginMode.LoginOrCreate : LoginMode.Login;
            Remember = false;
            return true;
        }

        /// <summary>A login is ready to be sent (a name, and a password or the remembered key).</summary>
        public static bool Ready => !string.IsNullOrEmpty(Name) && (!string.IsNullOrEmpty(Password) || RememberedName != null && LoginCrypto.NameKey(RememberedName) == LoginCrypto.NameKey(Name));

        /// <summary>Forgets what was loaded (tests).</summary>
        public static void ResetForTests()
        {
            saved = null;
            loaded = false;
            Name = Password = "";
            Mode = LoginMode.Login;
            Remember = true;
            LastResult = null;
            LastError = null;
        }
    }
}
