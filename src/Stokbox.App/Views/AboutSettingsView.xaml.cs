using System.Windows;
using System.Windows.Controls;
using Stokbox.App.Services;

namespace Stokbox.App.Views
{
    public partial class AboutSettingsView : UserControl
    {
        public AboutSettingsView()
        {
            InitializeComponent();

            var logo = AppInfo.TryLoadLogo();
            if (logo != null)
            {
                LogoImage.Source = logo;
                LogoImage.Visibility = Visibility.Visible;
            }
        }
    }
}
