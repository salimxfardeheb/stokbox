using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Stokbox.App.Views
{
    public partial class HistoryView : UserControl
    {
        public HistoryView()
        {
            InitializeComponent();

            // Opening the screen puts the cursor in the number field: a ticket number can be typed at once.
            Loaded += (sender, e) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => NumberBox.Focus()));
        }
    }
}
