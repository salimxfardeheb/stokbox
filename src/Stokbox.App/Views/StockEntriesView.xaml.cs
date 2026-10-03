using System;
using System.Windows.Controls;
using System.Windows.Threading;
using Stokbox.App.ViewModels;

namespace Stokbox.App.Views
{
    public partial class StockEntriesView : UserControl
    {
        private StockEntriesViewModel _viewModel;

        public StockEntriesView()
        {
            InitializeComponent();

            // The ViewModel outlives the view (rebuilt at each navigation): subscribe only while loaded.
            Loaded += (sender, e) =>
            {
                Attach(DataContext as StockEntriesViewModel);
                FocusCurrentField();
            };
            Unloaded += (sender, e) => Attach(null);
        }

        private void Attach(StockEntriesViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.QuantityFocusRequested -= OnQuantityFocusRequested;
            }

            _viewModel = viewModel;

            if (_viewModel != null)
            {
                _viewModel.QuantityFocusRequested += OnQuantityFocusRequested;
            }
        }

        // Opening the screen puts the cursor where the work continues, so that a scan can follow at once.
        private void FocusCurrentField()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (_viewModel != null && _viewModel.HasSelectedProduct)
                {
                    FocusQuantityBox();
                }
                else
                {
                    SearchBox.FocusSearchBox();
                }
            }));
        }

        private void OnQuantityFocusRequested(object sender, EventArgs e)
        {
            // Deferred: the quantity panel becomes visible in the same pass and cannot take the focus before layout.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusQuantityBox));
        }

        private void FocusQuantityBox()
        {
            QuantityBox.Focus();
            QuantityBox.SelectAll();
        }
    }
}
