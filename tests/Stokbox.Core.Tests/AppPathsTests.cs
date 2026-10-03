using System;
using System.IO;
using Xunit;

namespace Stokbox.Core.Tests
{
    public class AppPathsTests
    {
        [Fact]
        public void Database_and_logs_are_located_under_the_data_directory()
        {
            var root = Path.Combine(Path.GetTempPath(), "stokbox-paths");

            var paths = new AppPaths(root);

            Assert.Equal(Path.Combine(root, "stokbox.db"), paths.DatabaseFilePath);
            Assert.Equal(Path.Combine(root, "logs"), paths.LogsDirectory);
            Assert.Equal(Path.Combine(root, "backups"), paths.BackupsDirectory);
        }

        [Fact]
        public void Default_paths_are_under_ProgramData_Stokbox()
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            var paths = AppPaths.CreateDefault();

            Assert.Equal(Path.Combine(programData, "Stokbox"), paths.DataDirectory);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Data_directory_is_required(string dataDirectory)
        {
            Assert.Throws<ArgumentException>(() => new AppPaths(dataDirectory));
        }
    }
}
