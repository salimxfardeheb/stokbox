using System.Windows;
using Stokbox.App.ViewModels;

namespace Stokbox.App.Views
{
    public partial class ProductFormWindow : Window
    {
        public ProductFormWindow(ProductFormViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.Saved += (sender, e) => DialogResult = true;
        }
    }
}
