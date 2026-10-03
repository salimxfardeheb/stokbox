using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Stokbox.App.Views
{
    public partial class LabelsView : UserControl
    {
        public LabelsView()
        {
            InitializeComponent();

            // Opening the screen puts the cursor in the search field.
            Loaded += (sender, e) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => SearchBox.Focus()));
        }
    }
}
