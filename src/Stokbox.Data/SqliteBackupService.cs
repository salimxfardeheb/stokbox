using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using Dapper;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.Data
{
    /// <summary>
    /// Backups made with the SQLite online backup API: the copy is consistent even while the database is in use,
    /// which a plain copy of the open file (and of its write-ahead log) would not guarantee.
    /// </summary>
    public sealed class SqliteBackupService : IBackupService
    {
        /// <summary>
        /// Number of automatic backups kept; older ones are deleted.
        /// </summary>
        public const int KeptAutomaticBackups = 7;

        private const string FilePrefix = "stokbox_";
        private const string FileExtension = ".db";
        private const string MainDatabase = "main";

        private readonly SqliteConnectionFactory _connectionFactory;
        private readonly int _latestSchemaVersion;
        private readonly Func<DateTime> _localNow;

        public SqliteBackupService(SqliteConnectionFactory connectionFactory, string backupsDirectory, int latestSchemaVersion)
            : this(connectionFactory, backupsDirectory, latestSchemaVersion, () => DateTime.Now)
        {
        }

        public SqliteBackupService(
            SqliteConnectionFactory connectionFactory,
            string backupsDirectory,
            int latestSchemaVersion,
            Func<DateTime> localNow)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            if (string.IsNullOrWhiteSpace(backupsDirectory))
            {
                throw new ArgumentException("Le dossier des sauvegardes est obligatoire.", nameof(backupsDirectory));
            }

            BackupsDirectory = backupsDirectory;
            _latestSchemaVersion = latestSchemaVersion;
            _localNow = localNow ?? throw new ArgumentNullException(nameof(localNow));
        }

        public string BackupsDirectory { get; }

        public string BackupTo(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new BusinessRuleException("Choisissez le dossier de la sauvegarde.");
            }

            string filePath = null;
            try
            {
                Directory.CreateDirectory(directory);
                filePath = NewBackupPath(directory);

                using (var source = _connectionFactory.Open())
                using (var destination = OpenFile(filePath, false))
                {
                    source.BackupDatabase(destination, MainDatabase, MainDatabase, -1, null, 0);

                    // A single self-contained file: without this the copy would stay in write-ahead-log mode
                    // and need its "-wal" side file to be carried along.
                    destination.ExecuteScalar<string>("PRAGMA journal_mode = DELETE");
                }
            }
            catch (Exception ex) when (ex is SQLiteException || ex is IOException || ex is UnauthorizedAccessException)
            {
                DeleteQuietly(filePath);
                throw new BusinessRuleException(
                    "La sauvegarde a échoué. Vérifiez que le dossier est accessible et qu'il reste de la place. (" + ex.Message + ")");
            }

            if (!IsSound(filePath))
            {
                DeleteQuietly(filePath);
                throw new BusinessRuleException("La sauvegarde produite n'est pas intègre : elle a été supprimée. Recommencez.");
            }

            return filePath;
        }

        public string BackupAutomatically()
        {
            var filePath = BackupTo(BackupsDirectory);
            DeleteOldBackups();
            return filePath;
        }

        public void CheckRestorable(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new BusinessRuleException("Ce fichier de sauvegarde n'existe pas.");
            }

            if (string.Equals(Path.GetFullPath(filePath), _connectionFactory.DatabaseFilePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException("Ce fichier est la base en cours d'utilisation, pas une sauvegarde.");
            }

            const string NotABackup = "Ce fichier est corrompu ou n'est pas une sauvegarde Stokbox.";

            int version;
            try
            {
                using (var connection = OpenFile(filePath, true))
                {
                    if (!IsIntegrityOk(connection))
                    {
                        throw new BusinessRuleException(NotABackup);
                    }

                    var stokboxTables = connection.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('settings', 'products', 'sales')");
                    version = connection.ExecuteScalar<int>("PRAGMA user_version");
                    if (stokboxTables != 3 || version < 1)
                    {
                        throw new BusinessRuleException(NotABackup);
                    }
                }
            }
            catch (SQLiteException)
            {
                throw new BusinessRuleException(NotABackup);
            }

            if (version > _latestSchemaVersion)
            {
                throw new BusinessRuleException(
                    "Cette sauvegarde vient d'une version plus récente de Stokbox. Mettez l'application à jour avant de la restaurer.");
            }
        }

        public string Restore(string filePath)
        {
            CheckRestorable(filePath);

            // Whatever happens next, the data replaced can be brought back from this file.
            var safetyBackup = BackupAutomatically();

            try
            {
                // The backup API again, in the other direction: the database is replaced in one go,
                // through SQLite, write-ahead log included.
                using (var source = OpenFile(filePath, true))
                using (var destination = _connectionFactory.Open())
                {
                    source.BackupDatabase(destination, MainDatabase, MainDatabase, -1, null, 0);
                }
            }
            catch (SQLiteException ex)
            {
                throw new BusinessRuleException(
                    "La restauration a échoué ; la base actuelle a été sauvegardée dans " + safetyBackup + ". (" + ex.Message + ")");
            }

            return safetyBackup;
        }

        // "stokbox_AAAAMMJJ_HHMMSS.db"; a second backup within the same second gets a numbered suffix.
        private string NewBackupPath(string directory)
        {
            var name = FilePrefix + _localNow().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(directory, name + FileExtension);
            for (var suffix = 2; File.Exists(path); suffix++)
            {
                path = Path.Combine(directory, name + "_" + suffix.ToString(CultureInfo.InvariantCulture) + FileExtension);
            }

            return path;
        }

        // File names sort by date, so the most recent backups are the last ones.
        private void DeleteOldBackups()
        {
            var obsolete = Directory
                .GetFiles(BackupsDirectory, FilePrefix + "*" + FileExtension)
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .Skip(KeptAutomaticBackups);

            foreach (var path in obsolete)
            {
                DeleteQuietly(path);
            }
        }

        private static bool IsSound(string filePath)
        {
            try
            {
                using (var connection = OpenFile(filePath, true))
                {
                    return IsIntegrityOk(connection);
                }
            }
            catch (SQLiteException)
            {
                return false;
            }
        }

        private static bool IsIntegrityOk(SQLiteConnection connection)
        {
            return string.Equals(
                connection.Query<string>("PRAGMA integrity_check").FirstOrDefault(),
                "ok",
                StringComparison.OrdinalIgnoreCase);
        }

        // Plain connections, without the settings of the application database: these files are only copied or read.
        private static SQLiteConnection OpenFile(string filePath, bool readOnly)
        {
            var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder
            {
                DataSource = filePath,
                Version = 3,
                ReadOnly = readOnly,
                FailIfMissing = readOnly,
                Pooling = false
            }.ToString());

            try
            {
                connection.Open();
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private static void DeleteQuietly(string filePath)
        {
            try
            {
                if (filePath != null && File.Exists(filePath))
                {
                    File.Delete(filePath);
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
