using System;
using System.Data.SQLite;
using System.IO;

namespace Stokbox.Data.Tests
{
    /// <summary>
    /// A database file in its own temporary folder, deleted when the test ends.
    /// The folder does not exist until the first connection is opened.
    /// </summary>
    public sealed class TempDatabase : IDisposable
    {
        public TempDatabase()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "stokbox-tests", Guid.NewGuid().ToString("N"));
            FilePath = Path.Combine(DirectoryPath, "stokbox.db");
            ConnectionFactory = new SqliteConnectionFactory(FilePath);
        }

        public string DirectoryPath { get; }

        public string FilePath { get; }

        public SqliteConnectionFactory ConnectionFactory { get; }

        public SQLiteConnection Open()
        {
            return ConnectionFactory.Open();
        }

        public void Dispose()
        {
            try
            {
                // System.Data.SQLite may keep the file handle until finalizers have run.
                SQLiteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
