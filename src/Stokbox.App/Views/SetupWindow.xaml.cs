using System.Windows;
using Stokbox.App.ViewModels;

namespace Stokbox.App.Views
{
    public partial class SetupWindow : Window
    {
        private readonly SetupViewModel _viewModel;

        public SetupWindow(SetupViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
        }

        // A PasswordBox does not expose its content to bindings, on purpose: it is read only when the form is submitted.
        private void OnStartClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.TryComplete(PasswordBox.Password, ConfirmationBox.Password))
            {
                DialogResult = true;
            }
        }
    }
}
