using System.Windows;
using Stokbox.App.ViewModels;

namespace Stokbox.App.Views
{
    public partial class CategoriesWindow : Window
    {
        public CategoriesWindow(CategoriesViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
