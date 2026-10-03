using CommunityToolkit.Mvvm.ComponentModel;
using Stokbox.Core.Services;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Login at startup. The password itself never stays in the ViewModel: the window hands it over for the check.
    /// </summary>
    public sealed class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService;
        private string _error;

        public LoginViewModel(AuthService authService)
        {
            _authService = authService;
        }

        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
        }

        public bool TryLogin(string password)
        {
            if (_authService.VerifyPassword(password))
            {
                Error = null;
                return true;
            }

            Error = string.IsNullOrEmpty(password) ? "Saisissez le mot de passe." : "Mot de passe incorrect.";
            return false;
        }

        public void ClearError()
        {
            Error = null;
        }
    }
}
