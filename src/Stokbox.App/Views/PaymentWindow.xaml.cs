using System.Windows;
using Stokbox.App.ViewModels;

namespace Stokbox.App.Views
{
    public partial class PaymentWindow : Window
    {
        public PaymentWindow(PaymentViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.Completed += (sender, e) => DialogResult = true;

            // The amount is selected: typing replaces it, Enter alone accepts the exact total.
            Loaded += (sender, e) =>
            {
                ReceivedBox.Focus();
                ReceivedBox.SelectAll();
            };
        }
    }
}
