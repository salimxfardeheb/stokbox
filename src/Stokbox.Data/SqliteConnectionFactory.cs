using System;
using System.Data.SQLite;
using System.IO;

namespace Stokbox.Data
{
    /// <summary>
    /// Opens connections to the single Stokbox database file, always with the same safety settings.
    /// </summary>
    public sealed class SqliteConnectionFactory
    {
        private readonly string _databaseFilePath;
        private readonly string _connectionString;

        public SqliteConnectionFactory(string databaseFilePath)
        {
            if (string.IsNullOrWhiteSpace(databaseFilePath))
            {
                throw new ArgumentException("Le chemin de la base de données est obligatoire.", nameof(databaseFilePath));
            }

            _databaseFilePath = Path.GetFullPath(databaseFilePath);
            _connectionString = new SQLiteConnectionStringBuilder
            {
                DataSource = _databaseFilePath,
                Version = 3,
                ForeignKeys = true,
                JournalMode = SQLiteJournalModeEnum.Wal,
                SyncMode = SynchronizationModes.Full
            }.ToString();
        }

        public string DatabaseFilePath => _databaseFilePath;

        /// <summary>
        /// Returns an open connection; the caller disposes it. Creates the folder and the file if absent.
        /// </summary>
        public SQLiteConnection Open()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_databaseFilePath));

            var connection = new SQLiteConnection(_connectionString);
            try
            {
                connection.Open();
                FoldFunction.Bind(connection);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
    }
}
