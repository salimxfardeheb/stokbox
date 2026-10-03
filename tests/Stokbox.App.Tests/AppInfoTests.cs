using System.Text.RegularExpressions;
using Stokbox.App.Services;
using Stokbox.App.ViewModels;
using Stokbox.Core;
using Xunit;

namespace Stokbox.App.Tests
{
    public class AppInfoTests
    {
        [Fact]
        public void The_version_is_three_numbers_without_build_metadata()
        {
            Assert.Matches(new Regex(@"^\d+\.\d+\.\d+$"), AppInfo.Version);
        }

        [Fact]
        public void Every_assembly_carries_the_single_version_of_the_product()
        {
            // Core is built from the same Directory.Build.props: one number for the whole product.
            Assert.Equal(typeof(AppPaths).Assembly.GetName().Version.ToString(3), AppInfo.Version);
            Assert.Equal(typeof(Data.SqliteConnectionFactory).Assembly.GetName().Version.ToString(3), AppInfo.Version);
        }

        [Fact]
        public void The_about_screen_shows_the_version_and_where_the_data_is()
        {
            var about = new AboutViewModel(new AppPaths(@"C:\ProgramData\Stokbox"));

            Assert.Equal("Version " + AppInfo.Version, about.VersionText);
            Assert.Equal(@"C:\ProgramData\Stokbox\stokbox.db", about.DatabaseFilePath);
            Assert.Equal(@"C:\ProgramData\Stokbox\backups", about.BackupsDirectory);
            Assert.Equal(@"C:\ProgramData\Stokbox\logs", about.LogsDirectory);
        }
    }
}
