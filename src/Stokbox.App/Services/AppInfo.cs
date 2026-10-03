using System;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Stokbox.App.Services
{
    /// <summary>
    /// Identity of the application: its version and its logo.
    /// </summary>
    public static class AppInfo
    {
        // The logo is linked into the executable from the assets folder at the root of the repository.
        private const string LogoUri = "pack://application:,,,/Stokbox;component/Assets/logo.png";

        /// <summary>
        /// The version set once for the whole product in Directory.Build.props, e.g. "1.0.0".
        /// </summary>
        public static string Version
        {
            get
            {
                var assembly = typeof(AppInfo).Assembly;
                var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (string.IsNullOrWhiteSpace(informational))
                {
                    return assembly.GetName().Version.ToString(3);
                }

                // Build metadata, if any, is not part of what the user is shown.
                var metadataStart = informational.IndexOf('+');
                return metadataStart < 0 ? informational : informational.Substring(0, metadataStart);
            }
        }

        /// <summary>
        /// The logo of the application; null when the build does not embed one.
        /// </summary>
        public static ImageSource TryLoadLogo()
        {
            try
            {
                var logo = new BitmapImage();
                logo.BeginInit();
                logo.UriSource = new Uri(LogoUri, UriKind.Absolute);
                logo.CacheOption = BitmapCacheOption.OnLoad;
                logo.EndInit();
                logo.Freeze();
                return logo;
            }
            catch (Exception)
            {
                // No logo embedded, or unreadable: the screens fall back to the name in text.
                return null;
            }
        }
    }
}
