using System;
using Stokbox.Core.Services;
using Xunit;

namespace Stokbox.Core.Tests
{
    public class PasswordHasherTests
    {
        [Fact]
        public void A_password_is_verified_against_its_hash()
        {
            var hash = PasswordHasher.Hash("Boutique2026!");

            Assert.True(PasswordHasher.Verify("Boutique2026!", hash));
        }

        [Theory]
        [InlineData("boutique2026!")]
        [InlineData("Boutique2026")]
        [InlineData("Boutique2026! ")]
        [InlineData("")]
        [InlineData(null)]
        public void Another_password_is_refused(string attempt)
        {
            var hash = PasswordHasher.Hash("Boutique2026!");

            Assert.False(PasswordHasher.Verify(attempt, hash));
        }

        [Fact]
        public void The_hash_is_PBKDF2_SHA256_with_100000_iterations_a_16_byte_salt_and_no_trace_of_the_password()
        {
            var parts = PasswordHasher.Hash("Boutique2026!").Split(':');

            Assert.Equal(4, parts.Length);
            Assert.Equal("PBKDF2-SHA256", parts[0]);
            Assert.True(int.Parse(parts[1]) >= 100000);
            Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
            Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
            Assert.DoesNotContain("Boutique2026!", string.Join(":", parts));
        }

        [Fact]
        public void The_same_password_hashed_twice_gives_two_different_hashes_that_both_verify()
        {
            var first = PasswordHasher.Hash("Boutique2026!");
            var second = PasswordHasher.Hash("Boutique2026!");

            Assert.NotEqual(first, second);
            Assert.NotEqual(first.Split(':')[2], second.Split(':')[2]);
            Assert.True(PasswordHasher.Verify("Boutique2026!", first));
            Assert.True(PasswordHasher.Verify("Boutique2026!", second));
        }

        [Fact]
        public void The_hash_is_the_standard_PBKDF2_HMAC_SHA256_derivation()
        {
            // Known vector: password "password", salt "saltSALTsaltSALT" (16 bytes), 100000 iterations, 32 bytes.
            // Recomputed here with the framework primitive: the stored value must be exactly that derivation.
            var salt = System.Text.Encoding.ASCII.GetBytes("saltSALTsaltSALT");
            byte[] expected;
            using (var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
                "password", salt, 100000, System.Security.Cryptography.HashAlgorithmName.SHA256))
            {
                expected = pbkdf2.GetBytes(32);
            }

            var stored = "PBKDF2-SHA256:100000:" + Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(expected);

            Assert.True(PasswordHasher.Verify("password", stored));
            Assert.False(PasswordHasher.Verify("Password", stored));
        }

        [Fact]
        public void A_tampered_hash_is_refused()
        {
            var parts = PasswordHasher.Hash("Boutique2026!").Split(':');
            var hash = Convert.FromBase64String(parts[3]);
            hash[hash.Length - 1] ^= 1;
            var salt = Convert.FromBase64String(parts[2]);
            salt[0] ^= 1;

            Assert.False(PasswordHasher.Verify("Boutique2026!", string.Join(":", parts[0], parts[1], parts[2], Convert.ToBase64String(hash))));
            Assert.False(PasswordHasher.Verify("Boutique2026!", string.Join(":", parts[0], parts[1], Convert.ToBase64String(salt), parts[3])));
            Assert.False(PasswordHasher.Verify("Boutique2026!", string.Join(":", parts[0], "99999", parts[2], parts[3])));

            // A truncated hash must not match on its first bytes only.
            var truncated = Convert.ToBase64String(Convert.FromBase64String(parts[3]), 0, 16);
            Assert.False(PasswordHasher.Verify("Boutique2026!", string.Join(":", parts[0], parts[1], parts[2], truncated)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Boutique2026!")]
        [InlineData("PBKDF2-SHA256:100000:abc")]
        [InlineData("PBKDF2-SHA256:x:AAAAAAAAAAAAAAAAAAAAAA==:AAAA")]
        [InlineData("PBKDF2-SHA256:0:AAAAAAAAAAAAAAAAAAAAAA==:AAAA")]
        [InlineData("PBKDF2-SHA256:100000:pas du base64:AAAA")]
        [InlineData("PBKDF2-SHA256:100000:AAAA:AAAA")]
        [InlineData("MD5:100000:AAAAAAAAAAAAAAAAAAAAAA==:AAAA")]
        public void A_missing_or_malformed_hash_matches_nothing(string storedHash)
        {
            Assert.False(PasswordHasher.Verify("Boutique2026!", storedHash));
        }

        [Fact]
        public void Accents_and_long_passwords_are_supported()
        {
            var password = "Épicerie d'Alger — mot de passe très long, avec des espaces et des accents éàù";

            Assert.True(PasswordHasher.Verify(password, PasswordHasher.Hash(password)));
        }
    }
}
