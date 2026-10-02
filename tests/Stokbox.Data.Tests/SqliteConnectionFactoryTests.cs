using System;
using System.IO;
using Dapper;
using Xunit;

namespace Stokbox.Data.Tests
{
    public class SqliteConnectionFactoryTests : IDisposable
    {
        private readonly TempDatabase _database = new TempDatabase();

        public void Dispose()
        {
            _database.Dispose();
        }

        [Fact]
        public void Open_creates_the_folder_and_the_database_file_when_absent()
        {
            Assert.False(Directory.Exists(_database.DirectoryPath));

            using (_database.Open())
            {
            }

            Assert.True(File.Exists(_database.FilePath));
        }

        [Fact]
        public void Every_connection_enforces_foreign_keys_with_WAL_and_full_synchronous()
        {
            // Opened twice: the settings must hold for every connection, not only the one creating the file.
            using (_database.Open())
            {
            }

            using (var connection = _database.Open())
            {
                Assert.Equal(1, connection.ExecuteScalar<int>("PRAGMA foreign_keys"));
                Assert.Equal("wal", connection.ExecuteScalar<string>("PRAGMA journal_mode"), ignoreCase: true);
                Assert.Equal(2, connection.ExecuteScalar<int>("PRAGMA synchronous"));
            }
        }
    }
}
