using System;
using System.Security.Cryptography;
using System.Text;

namespace RPG
{
    /// <summary>
    /// Names and passwords of online accounts (online phase 2, Docs/KeHoach-Online.md). The
    /// password never crosses the network: the player's machine turns it into a key with PBKDF2
    /// and the server's salt, and signs the server's one-time nonce with that key (HMAC-SHA256).
    /// The server keeps only the key, which opens this game's account and nothing else.
    /// </summary>
    public static class LoginCrypto
    {
        /// <summary>PBKDF2 rounds: a guess costs an attacker this much, a login costs the player's machine a few dozen ms.</summary>
        public const int Iterations = 60000;
        public const int KeyBytes = 32;
        public const int SaltBytes = 16;
        public const int NonceBytes = 16;

        public const int NameMin = 3;
        public const int NameMax = 16;
        public const int PasswordMin = 4;

        public static byte[] RandomBytes(int count)
        {
            var b = new byte[count];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return b;
        }

        /// <summary>The account key: PBKDF2-SHA256 of the password with the account's salt.</summary>
        public static byte[] DeriveKey(string password, byte[] salt, int iterations = Iterations)
        {
            using (var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password ?? ""), salt, iterations, HashAlgorithmName.SHA256))
                return kdf.GetBytes(KeyBytes);
        }

        /// <summary>What the player's machine sends back for a nonce: HMAC-SHA256(key, nonce).</summary>
        public static byte[] Proof(byte[] key, byte[] nonce)
        {
            using (var h = new HMACSHA256(key)) return h.ComputeHash(nonce);
        }

        /// <summary>Checks a proof in constant time.</summary>
        public static bool Verify(byte[] key, byte[] nonce, byte[] proof)
        {
            if (key == null || nonce == null || proof == null) return false;
            var expected = Proof(key, nonce);
            if (expected.Length != proof.Length) return false;
            int diff = 0;
            for (int i = 0; i < expected.Length; i++) diff |= expected[i] ^ proof[i];
            return diff == 0;
        }

        /// <summary>A name as it is shown: trimmed, single spaces, composed Vietnamese letters.</summary>
        public static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder(name.Length);
            bool space = false;
            foreach (char c in name.Trim().Normalize(NormalizationForm.FormC))
            {
                if (char.IsWhiteSpace(c))
                {
                    space = true;
                    continue;
                }
                if (space && sb.Length > 0) sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The account's key on disk: two names that only differ in case are the same account.
        /// Hex of a hash, so any Vietnamese name makes a safe file name.
        /// </summary>
        public static string NameKey(string name)
        {
            string n = NormalizeName(name).ToLowerInvariant();
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(n));
                var sb = new StringBuilder(32);
                for (int i = 0; i < 16; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Why a character name cannot be used, or null when it can.</summary>
        public static string CheckName(string name)
        {
            string n = NormalizeName(name);
            if (n.Length < NameMin) return $"Tên cần ít nhất {NameMin} ký tự.";
            if (n.Length > NameMax) return $"Tên dài tối đa {NameMax} ký tự.";
            foreach (char c in n)
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '_' && c != '-' && c != '.')
                    return "Tên chỉ gồm chữ, số, dấu cách và _ - .";
            return null;
        }

        /// <summary>Why a password cannot be used, or null when it can.</summary>
        public static string CheckPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < PasswordMin) return $"Mật khẩu cần ít nhất {PasswordMin} ký tự.";
            if (password.Length > 64) return "Mật khẩu dài tối đa 64 ký tự.";
            return null;
        }
    }
}
