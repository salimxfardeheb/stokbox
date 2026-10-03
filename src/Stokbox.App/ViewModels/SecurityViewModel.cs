using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Settings > Security: change of the administrator password.
    /// The passwords never stay in the ViewModel: the view hands them over for the change.
    /// </summary>
    public sealed class SecurityViewModel : ObservableObject
    {
        private readonly AuthService _authService;

        private string _currentPasswordError;
        private string _newPasswordError;
        private string _confirmationError;
        private string _statusMessage;

        public SecurityViewModel(AuthService authService)
        {
            _authService = authService;
        }

        public string CurrentPasswordError
        {
            get => _currentPasswordError;
            private set => SetProperty(ref _currentPasswordError, value);
        }

        public string NewPasswordError
        {
            get => _newPasswordError;
            private set => SetProperty(ref _newPasswordError, value);
        }

        public string ConfirmationError
        {
            get => _confirmationError;
            private set => SetProperty(ref _confirmationError, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Clears the messages; called each time the screen is shown.
        /// </summary>
        public void Load()
        {
            ShowErrors(new ValidationError[0]);
            StatusMessage = null;
        }

        /// <returns>True when the password was changed.</returns>
        public bool TryChangePassword(string currentPassword, string newPassword, string confirmation)
        {
            try
            {
                _authService.ChangePassword(currentPassword, newPassword, confirmation);
            }
            catch (ValidationException ex)
            {
                ShowErrors(ex.Errors);
                StatusMessage = null;
                return false;
            }

            ShowErrors(new ValidationError[0]);
            StatusMessage = "Mot de passe modifié. Il sera demandé au prochain démarrage.";
            return true;
        }

        private void ShowErrors(IReadOnlyList<ValidationError> errors)
        {
            CurrentPasswordError = MessageFor(errors, AuthService.CurrentPasswordField);
            NewPasswordError = MessageFor(errors, AuthService.PasswordField);
            ConfirmationError = MessageFor(errors, AuthService.ConfirmationField);
        }

        private static string MessageFor(IReadOnlyList<ValidationError> errors, string field)
        {
            return errors.FirstOrDefault(error => error.Field == field)?.Message;
        }
    }
}
