using System.Windows;
using Stokbox.App.ViewModels;
using Stokbox.App.Views;

namespace Stokbox.App.Services
{
    public sealed class DialogService : IDialogService
    {
        private const string Caption = "Stokbox";

        public bool ShowProductForm(ProductFormViewModel viewModel)
        {
            var window = new ProductFormWindow(viewModel) { Owner = Application.Current.MainWindow };
            return window.ShowDialog() == true;
        }

        public void ShowCategories(CategoriesViewModel viewModel)
        {
            var window = new CategoriesWindow(viewModel) { Owner = Application.Current.MainWindow };
            window.ShowDialog();
        }

        public bool Confirm(string message)
        {
            return MessageBox.Show(
                Application.Current.MainWindow,
                message,
                Caption,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        public void ShowWarning(string message)
        {
            MessageBox.Show(Application.Current.MainWindow, message, Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
