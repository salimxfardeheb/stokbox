using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// First launch: the shop details and the creation of the administrator password.
    /// </summary>
    public sealed class SetupViewModel : ObservableObject
    {
        private readonly AuthService _authService;

        private string _shopName = string.Empty;
        private string _shopAddress = string.Empty;
        private string _shopPhone = string.Empty;
        private string _shopNameError;
        private string _passwordError;
        private string _confirmationError;
        private string _generalError;

        public SetupViewModel(AuthService authService)
        {
            _authService = authService;
        }

        public string ShopName
        {
            get => _shopName;
            set => SetProperty(ref _shopName, value);
        }

        public string ShopAddress
        {
            get => _shopAddress;
            set => SetProperty(ref _shopAddress, value);
        }

        public string ShopPhone
        {
            get => _shopPhone;
            set => SetProperty(ref _shopPhone, value);
        }

        public string ShopNameError
        {
            get => _shopNameError;
            private set => SetProperty(ref _shopNameError, value);
        }

        public string PasswordError
        {
            get => _passwordError;
            private set => SetProperty(ref _passwordError, value);
        }

        public string ConfirmationError
        {
            get => _confirmationError;
            private set => SetProperty(ref _confirmationError, value);
        }

        public string GeneralError
        {
            get => _generalError;
            private set => SetProperty(ref _generalError, value);
        }

        /// <returns>True when the shop and the password are saved: the application can open.</returns>
        public bool TryComplete(string password, string confirmation)
        {
            var input = new FirstRunInput
            {
                ShopName = ShopName,
                ShopAddress = ShopAddress,
                ShopPhone = ShopPhone,
                Password = password,
                PasswordConfirmation = confirmation
            };

            var errors = _authService.ValidateFirstRun(input);
            ShowErrors(errors, null);
            if (errors.Count > 0)
            {
                return false;
            }

            try
            {
                _authService.CompleteFirstRun(input);
                return true;
            }
            catch (ValidationException ex)
            {
                ShowErrors(ex.Errors, null);
            }
            catch (BusinessRuleException ex)
            {
                ShowErrors(new ValidationError[0], ex.Message);
            }

            return false;
        }

        private void ShowErrors(IReadOnlyList<ValidationError> errors, string generalError)
        {
            ShopNameError = MessageFor(errors, AuthService.ShopNameField);
            PasswordError = MessageFor(errors, AuthService.PasswordField);
            ConfirmationError = MessageFor(errors, AuthService.ConfirmationField);
            GeneralError = generalError;
        }

        private static string MessageFor(IReadOnlyList<ValidationError> errors, string field)
        {
            return errors.FirstOrDefault(error => error.Field == field)?.Message;
        }
    }
}
