using System.Linq;
using Stokbox.Core.Services;
using Stokbox.Core.Tests.Fakes;
using Stokbox.Core.Validation;
using Xunit;

namespace Stokbox.Core.Tests
{
    public class AuthServiceTests
    {
        private readonly InMemorySettingsRepository _settings = new InMemorySettingsRepository();
        private readonly ReceiptSettingsService _shop;
        private readonly AuthService _auth;

        public AuthServiceTests()
        {
            _shop = new ReceiptSettingsService(_settings);
            _auth = new AuthService(_settings, _shop);
        }

        [Fact]
        public void A_new_installation_has_no_password()
        {
            Assert.False(_auth.IsConfigured);
            Assert.False(_auth.VerifyPassword(""));
            Assert.False(_auth.VerifyPassword("Boutique2026!"));
        }

        [Fact]
        public void The_first_launch_saves_the_shop_and_creates_the_password()
        {
            _auth.CompleteFirstRun(ValidInput());

            Assert.True(_auth.IsConfigured);
            Assert.True(_auth.VerifyPassword("Boutique2026!"));
            Assert.False(_auth.VerifyPassword("boutique2026!"));

            var shop = _shop.Get();
            Assert.Equal("Boutique El Baraka", shop.ShopName);
            Assert.Equal("12 rue Didouche Mourad, Alger", shop.ShopAddress);
            Assert.Equal("021 00 00 00", shop.ShopPhone);
        }

        [Fact]
        public void The_password_is_never_stored_in_clear()
        {
            _auth.CompleteFirstRun(ValidInput());

            Assert.DoesNotContain(_settings.Values.Values, value => value.Contains("Boutique2026!"));
            Assert.StartsWith("PBKDF2-SHA256:", _settings.Values["admin.password_hash"]);
        }

        [Fact]
        public void The_first_launch_keeps_a_receipt_printer_already_chosen()
        {
            var shop = _shop.Get();
            shop.PrinterName = "Ticket 80 mm";
            _shop.Save(shop);

            _auth.CompleteFirstRun(ValidInput());

            Assert.Equal("Ticket 80 mm", _shop.Get().PrinterName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void The_shop_name_is_required(string shopName)
        {
            var input = ValidInput();
            input.ShopName = shopName;

            AssertRefused(input, AuthService.ShopNameField);
        }

        [Fact]
        public void The_address_and_the_phone_are_optional()
        {
            var input = ValidInput();
            input.ShopAddress = null;
            input.ShopPhone = "";

            Assert.Empty(_auth.ValidateFirstRun(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1234567")]
        [InlineData("        ")]
        public void A_password_of_less_than_8_characters_is_refused(string password)
        {
            var input = ValidInput();
            input.Password = password;
            input.PasswordConfirmation = password;

            AssertRefused(input, AuthService.PasswordField);
        }

        [Fact]
        public void A_password_of_8_characters_is_accepted()
        {
            var input = ValidInput();
            input.Password = "12345678";
            input.PasswordConfirmation = "12345678";

            Assert.Empty(_auth.ValidateFirstRun(input));
        }

        [Fact]
        public void A_password_of_more_than_128_characters_is_refused()
        {
            var input = ValidInput();
            input.Password = new string('a', 129);
            input.PasswordConfirmation = input.Password;

            AssertRefused(input, AuthService.PasswordField);
        }

        [Theory]
        [InlineData("Boutique2026")]
        [InlineData("boutique2026!")]
        [InlineData("")]
        [InlineData(null)]
        public void The_confirmation_must_match_the_password(string confirmation)
        {
            var input = ValidInput();
            input.PasswordConfirmation = confirmation;

            AssertRefused(input, AuthService.ConfirmationField);
        }

        [Fact]
        public void Every_faulty_field_of_the_first_launch_is_reported_at_once()
        {
            var errors = _auth.ValidateFirstRun(new FirstRunInput());

            Assert.Equal(new[] { AuthService.ShopNameField, AuthService.PasswordField }, errors.Select(e => e.Field));
        }

        [Fact]
        public void The_first_launch_cannot_be_done_twice()
        {
            _auth.CompleteFirstRun(ValidInput());
            var again = ValidInput();
            again.Password = "AutreMotDePasse";
            again.PasswordConfirmation = "AutreMotDePasse";

            Assert.Throws<BusinessRuleException>(() => _auth.CompleteFirstRun(again));
            Assert.True(_auth.VerifyPassword("Boutique2026!"));
            Assert.False(_auth.VerifyPassword("AutreMotDePasse"));
        }

        [Fact]
        public void The_password_is_changed_when_the_current_one_is_given()
        {
            _auth.CompleteFirstRun(ValidInput());

            _auth.ChangePassword("Boutique2026!", "NouveauSecret9", "NouveauSecret9");

            Assert.True(_auth.VerifyPassword("NouveauSecret9"));
            Assert.False(_auth.VerifyPassword("Boutique2026!"));
        }

        [Theory]
        [InlineData("mauvais")]
        [InlineData("")]
        [InlineData(null)]
        public void Changing_the_password_requires_the_current_one(string currentPassword)
        {
            _auth.CompleteFirstRun(ValidInput());

            var error = Assert.Throws<ValidationException>(
                () => _auth.ChangePassword(currentPassword, "NouveauSecret9", "NouveauSecret9"));

            Assert.Equal(AuthService.CurrentPasswordField, error.Errors.Single().Field);
            Assert.True(_auth.VerifyPassword("Boutique2026!"));
            Assert.False(_auth.VerifyPassword("NouveauSecret9"));
        }

        [Fact]
        public void The_new_password_follows_the_same_rules()
        {
            _auth.CompleteFirstRun(ValidInput());

            var tooShort = Assert.Throws<ValidationException>(() => _auth.ChangePassword("Boutique2026!", "court", "court"));
            var mismatch = Assert.Throws<ValidationException>(
                () => _auth.ChangePassword("Boutique2026!", "NouveauSecret9", "NouveauSecret8"));

            Assert.Equal(AuthService.PasswordField, tooShort.Errors.Single().Field);
            Assert.Equal(AuthService.ConfirmationField, mismatch.Errors.Single().Field);
            Assert.True(_auth.VerifyPassword("Boutique2026!"));
        }

        private static FirstRunInput ValidInput()
        {
            return new FirstRunInput
            {
                ShopName = "Boutique El Baraka",
                ShopAddress = "12 rue Didouche Mourad, Alger",
                ShopPhone = "021 00 00 00",
                Password = "Boutique2026!",
                PasswordConfirmation = "Boutique2026!"
            };
        }

        private void AssertRefused(FirstRunInput input, string field)
        {
            var error = Assert.Single(_auth.ValidateFirstRun(input));
            Assert.Equal(field, error.Field);
            Assert.Throws<ValidationException>(() => _auth.CompleteFirstRun(input));
            Assert.False(_auth.IsConfigured);
        }
    }
}
