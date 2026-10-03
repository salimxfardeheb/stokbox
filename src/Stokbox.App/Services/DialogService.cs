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

        public bool ShowPayment(PaymentViewModel viewModel)
        {
            var window = new PaymentWindow(viewModel) { Owner = Application.Current.MainWindow };
            return window.ShowDialog() == true;
        }

        public bool ShowReturn(ReturnViewModel viewModel)
        {
            var window = new ReturnWindow(viewModel) { Owner = Application.Current.MainWindow };
            return window.ShowDialog() == true;
        }

        public bool Ask(string headline, string question)
        {
            var window = new AskWindow(headline, question) { Owner = Application.Current.MainWindow };
            return window.ShowDialog() == true;
        }

        public void ShowWarning(string message)
        {
            MessageBox.Show(Application.Current.MainWindow, message, Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
