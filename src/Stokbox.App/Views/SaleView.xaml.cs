using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Stokbox.App.ViewModels;

namespace Stokbox.App.Views
{
    public partial class SaleView : UserControl
    {
        public SaleView()
        {
            InitializeComponent();

            // The cursor is in the search field as soon as the screen appears: a scan can follow at once.
            Loaded += (sender, e) => FocusSearchLater();
        }

        private SaleViewModel ViewModel => DataContext as SaleViewModel;

        private void FocusSearchLater()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(SearchBox.FocusSearchBox));
        }

        // Shortcuts are caught here, before the search field sees the key.
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            if (e.Key == Key.F2)
            {
                // A quantity being typed is applied before the payment.
                SearchBox.FocusSearchBox();
                viewModel.CheckoutCommand.Execute(null);
                e.Handled = true;
                return;
            }

            var quantityBox = GetQuantityBox(e.OriginalSource);
            if (quantityBox != null)
            {
                if (e.Key == Key.Enter)
                {
                    // Leaving the cell applies the quantity typed.
                    SearchBox.FocusSearchBox();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    quantityBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                    SearchBox.FocusSearchBox();
                    e.Handled = true;
                }

                return;
            }

            // While a product is being searched, the keys belong to the search field.
            if (IsSearching(viewModel))
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Delete:
                    viewModel.RemoveSelectedCommand.Execute(null);
                    break;
                case Key.Up:
                    viewModel.MoveSelection(-1);
                    break;
                case Key.Down:
                    viewModel.MoveSelection(1);
                    break;
                case Key.Escape:
                    viewModel.ClearCartCommand.Execute(null);
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }

        // "+" and "-" are read as typed characters: the keys that produce them differ between keyboards (AZERTY, numeric pad).
        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null || GetQuantityBox(e.OriginalSource) != null || IsSearching(viewModel))
            {
                return;
            }

            if (e.Text == "+")
            {
                viewModel.IncreaseCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Text == "-")
            {
                viewModel.DecreaseCommand.Execute(null);
                e.Handled = true;
            }
        }

        private static bool IsSearching(SaleViewModel viewModel)
        {
            return viewModel.Search.IsResultListOpen || !string.IsNullOrEmpty(viewModel.Search.Text);
        }

        // The quantity cell of the cart, when the key comes from it; null when it comes from the search field.
        private TextBox GetQuantityBox(object source)
        {
            var textBox = source as TextBox;
            return textBox != null && CartGrid.IsAncestorOf(textBox) ? textBox : null;
        }

        // Clicking a line selects it but the keyboard goes back to the search field, unless a quantity is being typed.
        private void OnCartGridGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!(e.NewFocus is TextBox))
            {
                FocusSearchLater();
            }
        }

        private void OnQuantityBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private void OnCartSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CartGrid.SelectedItem != null)
            {
                CartGrid.ScrollIntoView(CartGrid.SelectedItem);
            }
        }
    }
}
