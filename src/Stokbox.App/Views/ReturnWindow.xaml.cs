using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Stokbox.App.ViewModels;

namespace Stokbox.App.Views
{
    public partial class ReturnWindow : Window
    {
        public ReturnWindow(ReturnViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.Completed += (sender, e) => DialogResult = true;
        }

        // The quantity is selected when its field is entered: typing replaces the 0.
        private void OnQuantityBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }
    }
}
