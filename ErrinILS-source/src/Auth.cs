// Admin password: salted PBKDF2 hash stored in errin-config.json. With nothing stored, the built-in default password applies.
using System;
using System.Security.Cryptography;
using System.Text;

namespace Errin
{
    public static class Auth
    {
        public const string DefaultHash = "f16959ae2d027f8562807b5d1dbbef305556f3fd4caaac6ac82679b33397ad72";   // SHA-256("errinils:admin123")
        public const int Iterations = 10000;

        static string Sha(string pw)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("errinils:" + pw))).Replace("-", "").ToLowerInvariant();
        }
        public static string NewSalt() { var b = new byte[16]; using (var r = RandomNumberGenerator.Create()) r.GetBytes(b); return Convert.ToBase64String(b); }
        public static string Hash(string pw, string saltB64, int iters)
        {
            using (var k = new Rfc2898DeriveBytes(pw ?? "", Convert.FromBase64String(saltB64), iters)) return Convert.ToBase64String(k.GetBytes(32));
        }
        static bool Same(string a, string b)
        {
            if (a.Length != b.Length) return false; int d = 0; for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i]; return d == 0;
        }
        /// <summary>salt/hash empty = the default password has never been changed.</summary>
        public static bool Verify(string pw, string saltB64, string hash, int iters)
        {
            if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(saltB64)) return Same(Sha(pw ?? ""), DefaultHash);
            try { return Same(Hash(pw, saltB64, iters > 0 ? iters : Iterations), hash); } catch { return false; }
        }
    }
}
