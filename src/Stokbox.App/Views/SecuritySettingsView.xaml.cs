using System.Windows;
using System.Windows.Controls;
using Stokbox.App.ViewModels;

namespace Stokbox.App.Views
{
    public partial class SecuritySettingsView : UserControl
    {
        public SecuritySettingsView()
        {
            InitializeComponent();
        }

        // PasswordBoxes do not expose their content to bindings, on purpose: they are read only when the form is submitted.
        private void OnChangePasswordClick(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as SecurityViewModel;
            if (viewModel == null)
            {
                return;
            }

            if (viewModel.TryChangePassword(CurrentPasswordBox.Password, NewPasswordBox.Password, ConfirmationBox.Password))
            {
                CurrentPasswordBox.Clear();
                NewPasswordBox.Clear();
                ConfirmationBox.Clear();
            }
        }
    }
}
