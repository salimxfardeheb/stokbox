using System.Windows;
using Stokbox.App.Services;
using Stokbox.App.ViewModels;

namespace Stokbox.App.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;

        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            Loaded += (sender, e) => PasswordBox.Focus();

            var logo = AppInfo.TryLoadLogo();
            if (logo != null)
            {
                LogoImage.Source = logo;
                LogoImage.Visibility = Visibility.Visible;
                NameText.Visibility = Visibility.Collapsed;
            }
        }

        // A PasswordBox does not expose its content to bindings, on purpose: it is read only at the moment of the check.
        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.TryLogin(PasswordBox.Password))
            {
                DialogResult = true;
                return;
            }

            PasswordBox.SelectAll();
            PasswordBox.Focus();
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearError();
        }
    }
}
