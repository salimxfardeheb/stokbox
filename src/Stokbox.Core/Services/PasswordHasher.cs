using System;
using System.Globalization;
using System.Security.Cryptography;

namespace Stokbox.Core.Services
{
    /// <summary>
    /// Password hashing with PBKDF2 (HMAC-SHA-256) and a random salt per password.
    /// Stored form: "PBKDF2-SHA256:iterations:salt:hash", salt and hash in base 64.
    /// </summary>
    public static class PasswordHasher
    {
        public const int Iterations = 100000;
        public const int SaltSize = 16;
        public const int HashSize = 32;

        private const string Scheme = "PBKDF2-SHA256";
        private const char Separator = ':';

        public static string Hash(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            var salt = new byte[SaltSize];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(salt);
            }

            return string.Join(
                Separator.ToString(),
                Scheme,
                Iterations.ToString(CultureInfo.InvariantCulture),
                Convert.ToBase64String(salt),
                Convert.ToBase64String(Derive(password, salt, Iterations, HashSize)));
        }

        /// <summary>
        /// True when the password is the one that was hashed. A missing or malformed hash matches nothing.
        /// </summary>
        public static bool Verify(string password, string storedHash)
        {
            if (password == null || string.IsNullOrEmpty(storedHash))
            {
                return false;
            }

            var parts = storedHash.Split(Separator);
            if (parts.Length != 4
                || parts[0] != Scheme
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
                || iterations < 1)
            {
                return false;
            }

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            // Rfc2898DeriveBytes refuses a salt shorter than 8 bytes. The hash must be whole:
            // a shortened one would otherwise match on its first bytes alone.
            if (salt.Length < 8 || expected.Length != HashSize)
            {
                return false;
            }

            return ConstantTimeEquals(Derive(password, salt, iterations, HashSize), expected);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations, int size)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(size);
            }
        }

        // Always reads every byte: the time taken tells nothing about where the first difference is.
        private static bool ConstantTimeEquals(byte[] left, byte[] right)
        {
            var difference = left.Length ^ right.Length;
            for (var i = 0; i < left.Length && i < right.Length; i++)
            {
                difference |= left[i] ^ right[i];
            }

            return difference == 0;
        }
    }
}
