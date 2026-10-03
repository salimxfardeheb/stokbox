using System;
using System.Collections.Generic;
using Stokbox.Core.Repositories;
using Stokbox.Core.Validation;

namespace Stokbox.Core.Services
{
    /// <summary>
    /// The single administrator password protecting the application.
    /// </summary>
    public sealed class AuthService
    {
        public const string ShopNameField = "ShopName";
        public const string PasswordField = "Password";
        public const string ConfirmationField = "Confirmation";
        public const string CurrentPasswordField = "CurrentPassword";

        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 128;

        private const string PasswordHashKey = "admin.password_hash";

        private readonly ISettingsRepository _settings;
        private readonly ReceiptSettingsService _shopSettings;

        public AuthService(ISettingsRepository settings, ReceiptSettingsService shopSettings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _shopSettings = shopSettings ?? throw new ArgumentNullException(nameof(shopSettings));
        }

        /// <summary>
        /// False until the administrator password has been created: the first launch.
        /// </summary>
        public bool IsConfigured => ReadHash() != null;

        /// <summary>
        /// At most one error per field; empty when the input is valid.
        /// </summary>
        public IReadOnlyList<ValidationError> ValidateFirstRun(FirstRunInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var errors = new List<ValidationError>();
            if (string.IsNullOrWhiteSpace(input.ShopName))
            {
                errors.Add(new ValidationError(ShopNameField, "Le nom de la boutique est obligatoire."));
            }

            AddPasswordErrors(errors, input.Password, input.PasswordConfirmation);
            return errors;
        }

        /// <summary>
        /// First launch: saves the shop details and creates the administrator password.
        /// </summary>
        public void CompleteFirstRun(FirstRunInput input)
        {
            if (IsConfigured)
            {
                throw new BusinessRuleException("Le mot de passe administrateur existe déjà.");
            }

            var errors = ValidateFirstRun(input);
            if (errors.Count > 0)
            {
                throw new ValidationException(errors);
            }

            var shop = _shopSettings.Get();
            shop.ShopName = input.ShopName;
            shop.ShopAddress = input.ShopAddress;
            shop.ShopPhone = input.ShopPhone;
            _shopSettings.Save(shop);

            // Saved last: an interrupted first launch simply starts again.
            SaveHash(input.Password);
        }

        public bool VerifyPassword(string password)
        {
            return PasswordHasher.Verify(password, ReadHash());
        }

        /// <summary>
        /// Replaces the password; the current one is required.
        /// </summary>
        public void ChangePassword(string currentPassword, string newPassword, string confirmation)
        {
            var errors = new List<ValidationError>();
            if (!VerifyPassword(currentPassword))
            {
                errors.Add(new ValidationError(CurrentPasswordField, "Le mot de passe actuel est incorrect."));
            }

            AddPasswordErrors(errors, newPassword, confirmation);
            if (errors.Count > 0)
            {
                throw new ValidationException(errors);
            }

            SaveHash(newPassword);
        }

        private static void AddPasswordErrors(List<ValidationError> errors, string password, string confirmation)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
            {
                errors.Add(new ValidationError(PasswordField, "Le mot de passe doit contenir au moins 8 caractères."));
            }
            else if (password.Length > MaxPasswordLength)
            {
                errors.Add(new ValidationError(PasswordField, "Le mot de passe ne peut pas dépasser 128 caractères."));
            }
            else if (password != confirmation)
            {
                errors.Add(new ValidationError(ConfirmationField, "La confirmation ne correspond pas au mot de passe."));
            }
        }

        private string ReadHash()
        {
            return _settings.GetAll().TryGetValue(PasswordHashKey, out var hash) && !string.IsNullOrWhiteSpace(hash)
                ? hash
                : null;
        }

        private void SaveHash(string password)
        {
            _settings.Save(new Dictionary<string, string> { { PasswordHashKey, PasswordHasher.Hash(password) } });
        }
    }
}
