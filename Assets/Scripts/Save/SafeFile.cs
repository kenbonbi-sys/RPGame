using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Text files that survive a crash mid-write: written to a temp file, then swapped in while the
    /// previous version is kept as .bak; a damaged file is read back from its .bak. Used by the
    /// local save slots and by the server's accounts and characters.
    /// </summary>
    public static class SafeFile
    {
        /// <summary>Writes to a temp file, then swaps it in and keeps the previous version as .bak.</summary>
        public static void Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            string bak = path + ".bak";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (!File.Exists(path))
            {
                File.Move(tmp, path);
                return;
            }
            try
            {
                File.Replace(tmp, path, bak);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(path, bak, true);
                File.Delete(path);
                File.Move(tmp, path);
            }
        }

        /// <summary>
        /// Reads <paramref name="path"/>, or its .bak when the file is missing or <paramref name="valid"/>
        /// rejects it. Null when neither is usable.
        /// </summary>
        public static string Read(string path, Func<string, bool> valid = null)
        {
            foreach (var p in new[] { path, path + ".bak" })
            {
                if (!File.Exists(p)) continue;
                try
                {
                    string text = File.ReadAllText(p);
                    if (valid == null || valid(text)) return text;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SafeFile] Cannot read {p}: {e.Message}");
                }
            }
            return null;
        }

        public static bool Exists(string path) => File.Exists(path) || File.Exists(path + ".bak");
    }
}
