using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Stokbox.App.ViewModels;

namespace Stokbox.App.Controls
{
    public partial class ProductSearchBox : UserControl
    {
        private ProductSearchViewModel _viewModel;

        public ProductSearchBox()
        {
            InitializeComponent();

            // The ViewModel outlives the control (screens are rebuilt at each navigation): subscribe only while loaded.
            Loaded += (sender, e) => Attach(DataContext as ProductSearchViewModel);
            Unloaded += (sender, e) => Attach(null);
            DataContextChanged += (sender, e) =>
            {
                if (IsLoaded)
                {
                    Attach(e.NewValue as ProductSearchViewModel);
                }
            };
        }

        /// <summary>
        /// Gives the keyboard focus to the search field and selects its text.
        /// </summary>
        public void FocusSearchBox()
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }

        private void Attach(ProductSearchViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.FocusRequested -= OnFocusRequested;
            }

            _viewModel = viewModel;

            if (_viewModel != null)
            {
                _viewModel.FocusRequested += OnFocusRequested;
            }
        }

        private void OnFocusRequested(object sender, EventArgs e)
        {
            FocusSearchBox();
        }

        private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var viewModel = DataContext as ProductSearchViewModel;
            if (viewModel == null)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Enter:
                    viewModel.Submit();
                    e.Handled = true;
                    break;
                case Key.Down:
                    viewModel.MoveSelection(1);
                    e.Handled = true;
                    break;
                case Key.Up:
                    viewModel.MoveSelection(-1);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    if (viewModel.IsResultListOpen)
                    {
                        viewModel.CloseResults();
                    }
                    else
                    {
                        viewModel.Clear();
                    }

                    e.Handled = true;
                    break;
            }
        }

        private void OnResultSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResultList.SelectedItem != null)
            {
                ResultList.ScrollIntoView(ResultList.SelectedItem);
            }
        }

        // Mouse users can click a result; the keyboard flow never needs it.
        private void OnResultListMouseUp(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as ProductSearchViewModel;
            if (viewModel != null && viewModel.SelectedResult != null && IsOnItem(e.OriginalSource as DependencyObject))
            {
                viewModel.Submit();
            }
        }

        private bool IsOnItem(DependencyObject source)
        {
            return source != null && ItemsControl.ContainerFromElement(ResultList, source) is ListBoxItem;
        }
    }
}
