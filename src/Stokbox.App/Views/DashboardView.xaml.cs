using System.Windows.Controls;
using System.Windows.Input;

namespace Stokbox.App.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();

            // The tables show all their rows and never scroll: the wheel over them scrolls the page.
            TopProductsGrid.PreviewMouseWheel += ScrollPage;
            CategoriesGrid.PreviewMouseWheel += ScrollPage;
        }

        private void ScrollPage(object sender, MouseWheelEventArgs e)
        {
            PageScroll.ScrollToVerticalOffset(PageScroll.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
