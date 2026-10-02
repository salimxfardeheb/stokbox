using System.Windows;
using Stokbox.App.ViewModels;

namespace Stokbox.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
