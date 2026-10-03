using Stokbox.App.Services;
using Stokbox.Core;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Settings > About: the version of the application and where its data is kept.
    /// </summary>
    public sealed class AboutViewModel
    {
        public AboutViewModel(AppPaths paths)
        {
            VersionText = "Version " + AppInfo.Version;
            DatabaseFilePath = paths.DatabaseFilePath;
            BackupsDirectory = paths.BackupsDirectory;
            LogsDirectory = paths.LogsDirectory;
        }

        public string VersionText { get; }

        public string DatabaseFilePath { get; }

        public string BackupsDirectory { get; }

        public string LogsDirectory { get; }
    }
}
