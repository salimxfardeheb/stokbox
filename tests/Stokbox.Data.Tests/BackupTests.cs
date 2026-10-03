using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Dapper;
using Stokbox.Core.Validation;
using Stokbox.Data.Migrations;
using Stokbox.Data.Repositories;
using Xunit;

namespace Stokbox.Data.Tests
{
    public class BackupTests : IDisposable
    {
        private readonly TempDatabase _database = new TempDatabase();
        private readonly string _backupsDirectory;
        private readonly string _usbDirectory;
        private readonly int _latestVersion;
        private readonly SqliteBackupService _service;

        // Each backup is taken one second after the previous one, as its name carries the time to the second.
        private DateTime _now = new DateTime(2026, 10, 3, 14, 5, 7);

        public BackupTests()
        {
            var migrations = new MigrationRunner(_database.ConnectionFactory);
            migrations.MigrateToLatest();
            _latestVersion = migrations.LatestVersion;

            _backupsDirectory = Path.Combine(_database.DirectoryPath, "backups");
            _usbDirectory = Path.Combine(_database.DirectoryPath, "cle-usb");
            _service = new SqliteBackupService(_database.ConnectionFactory, _backupsDirectory, _latestVersion, NextSecond);

            AddCategory("Épicerie");
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        // Sauvegarde.

        [Fact]
        public void A_manual_backup_is_one_sound_file_named_after_the_date_in_the_chosen_folder()
        {
            var path = _service.BackupTo(_usbDirectory);

            Assert.Equal(Path.Combine(_usbDirectory, "stokbox_20261003_140507.db"), path);
            Assert.Equal(new[] { path }, Directory.GetFiles(_usbDirectory));
            Assert.Equal(new[] { "Épicerie" }, CategoriesIn(path));

            using (var connection = OpenPlain(path))
            {
                Assert.Equal("ok", connection.ExecuteScalar<string>("PRAGMA integrity_check"));
                Assert.Equal(_latestVersion, connection.ExecuteScalar<int>("PRAGMA user_version"));

                // Self-contained: no write-ahead log to carry along with the file.
                Assert.Equal("delete", connection.ExecuteScalar<string>("PRAGMA journal_mode"), ignoreCase: true);
            }

            Assert.False(Directory.Exists(_backupsDirectory));
        }

        [Fact]
        public void Two_backups_in_the_same_second_do_not_overwrite_each_other()
        {
            var service = new SqliteBackupService(
                _database.ConnectionFactory, _backupsDirectory, _latestVersion, () => new DateTime(2026, 10, 3, 14, 5, 7));

            var first = service.BackupTo(_usbDirectory);
            var second = service.BackupTo(_usbDirectory);

            Assert.Equal("stokbox_20261003_140507.db", Path.GetFileName(first));
            Assert.Equal("stokbox_20261003_140507_2.db", Path.GetFileName(second));
            Assert.True(File.Exists(first));
        }

        [Fact]
        public void A_backup_that_cannot_be_written_is_reported_and_leaves_no_file()
        {
            // A file where the folder should be.
            Directory.CreateDirectory(_database.DirectoryPath);
            var notAFolder = Path.Combine(_database.DirectoryPath, "pas-un-dossier");
            File.WriteAllText(notAFolder, "x");

            Assert.Throws<BusinessRuleException>(() => _service.BackupTo(notAFolder));
            Assert.Throws<BusinessRuleException>(() => _service.BackupTo("  "));
        }

        [Fact]
        public void The_automatic_backup_goes_to_the_backups_folder()
        {
            var path = _service.BackupAutomatically();

            Assert.Equal(Path.Combine(_backupsDirectory, "stokbox_20261003_140507.db"), path);
            Assert.Equal(new[] { "Épicerie" }, CategoriesIn(path));
        }

        [Fact]
        public void Only_the_7_most_recent_automatic_backups_are_kept()
        {
            var created = Enumerable.Range(0, 10).Select(_ => _service.BackupAutomatically()).ToList();

            var kept = Directory.GetFiles(_backupsDirectory).OrderBy(path => path).ToList();

            Assert.Equal(7, kept.Count);
            Assert.Equal(created.Skip(3), kept);
            Assert.Equal("stokbox_20261003_140510.db", Path.GetFileName(kept.First()));
            Assert.Equal("stokbox_20261003_140516.db", Path.GetFileName(kept.Last()));
        }

        [Fact]
        public void Rotation_only_touches_the_backup_files()
        {
            Directory.CreateDirectory(_backupsDirectory);
            var note = Path.Combine(_backupsDirectory, "a-lire.txt");
            File.WriteAllText(note, "ne pas supprimer");

            for (var i = 0; i < 9; i++)
            {
                _service.BackupAutomatically();
            }

            Assert.True(File.Exists(note));
            Assert.Equal(7, Directory.GetFiles(_backupsDirectory, "stokbox_*.db").Length);
        }

        [Fact]
        public void Manual_backups_are_never_rotated()
        {
            for (var i = 0; i < 9; i++)
            {
                _service.BackupTo(_usbDirectory);
            }

            Assert.Equal(9, Directory.GetFiles(_usbDirectory).Length);
        }

        // Restauration.

        [Fact]
        public void A_backup_made_while_the_application_is_open_restores_correctly()
        {
            AddCategory("Boissons");

            string backup;

            // The application at work: a connection held open, a transaction in progress and not yet committed,
            // and committed changes still sitting in the write-ahead log.
            using (var busy = _database.Open())
            using (var pending = busy.BeginTransaction(System.Data.IsolationLevel.ReadCommitted))
            {
                busy.Execute("SELECT COUNT(*) FROM categories", transaction: pending);
                backup = _service.BackupTo(_usbDirectory);
                pending.Rollback();
            }

            // Life goes on after the backup.
            AddCategory("Hygiène");
            using (var connection = _database.Open())
            {
                connection.Execute("DELETE FROM categories WHERE name = 'Boissons'");
                connection.Execute("UPDATE barcode_sequence SET last_value = 99");
            }

            Assert.Equal(new[] { "Épicerie", "Hygiène" }, Categories());

            _service.Restore(backup);

            // Back to the exact state of the backup, read through fresh connections as after a restart.
            Assert.Equal(new[] { "Boissons", "Épicerie" }, Categories());
            using (var connection = _database.Open())
            {
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT last_value FROM barcode_sequence"));
                Assert.Equal("ok", connection.ExecuteScalar<string>("PRAGMA integrity_check"));
                Assert.Equal(_latestVersion, connection.ExecuteScalar<int>("PRAGMA user_version"));
                Assert.Equal("wal", connection.ExecuteScalar<string>("PRAGMA journal_mode"), ignoreCase: true);
            }

            // The restored database is fully usable.
            AddCategory("Papeterie");
            Assert.Equal(new[] { "Boissons", "Épicerie", "Papeterie" }, Categories());
        }

        [Fact]
        public void A_backup_taken_in_the_middle_of_an_uncommitted_change_does_not_contain_it()
        {
            string backup;
            using (var writer = _database.Open())
            using (var transaction = writer.BeginTransaction())
            {
                writer.Execute("INSERT INTO categories (name) VALUES ('Non validée')", transaction: transaction);
                backup = _service.BackupTo(_usbDirectory);
                transaction.Rollback();
            }

            Assert.Equal(new[] { "Épicerie" }, CategoriesIn(backup));
        }

        [Fact]
        public void Restoring_first_backs_up_the_current_database()
        {
            var backup = _service.BackupTo(_usbDirectory);
            AddCategory("Ajoutée après la sauvegarde");

            var safetyBackup = _service.Restore(backup);

            Assert.Equal(_backupsDirectory, Path.GetDirectoryName(safetyBackup));
            Assert.Equal(new[] { "Ajoutée après la sauvegarde", "Épicerie" }, CategoriesIn(safetyBackup));
            Assert.Equal(new[] { "Épicerie" }, Categories());

            // And that safety backup brings the replaced data back.
            _service.Restore(safetyBackup);
            Assert.Equal(new[] { "Ajoutée après la sauvegarde", "Épicerie" }, Categories());
        }

        [Fact]
        public void A_file_that_is_not_a_database_is_refused()
        {
            Directory.CreateDirectory(_usbDirectory);
            var garbage = Path.Combine(_usbDirectory, "stokbox_20260101_000000.db");
            File.WriteAllBytes(garbage, Enumerable.Range(0, 8192).Select(i => (byte)(i * 31)).ToArray());

            AssertRestoreRefused(garbage);
        }

        [Fact]
        public void A_corrupted_backup_is_refused()
        {
            for (var i = 0; i < 200; i++)
            {
                AddCategory("Catégorie " + i.ToString("000"));
            }

            var backup = _service.BackupTo(_usbDirectory);

            // Damage in the middle of the file: the header is intact, the content is not.
            var bytes = File.ReadAllBytes(backup);
            for (var i = bytes.Length / 2; i < bytes.Length / 2 + 6000 && i < bytes.Length; i++)
            {
                bytes[i] = 0xFF;
            }

            // The first page, which holds the table list, is damaged too.
            for (var i = 200; i < 2000; i++)
            {
                bytes[i] = 0xFF;
            }

            File.WriteAllBytes(backup, bytes);

            AssertRestoreRefused(backup);
        }

        [Fact]
        public void A_truncated_backup_is_refused()
        {
            for (var i = 0; i < 200; i++)
            {
                AddCategory("Catégorie " + i.ToString("000"));
            }

            var backup = _service.BackupTo(_usbDirectory);
            var bytes = File.ReadAllBytes(backup);
            File.WriteAllBytes(backup, bytes.Take(bytes.Length / 2).ToArray());

            AssertRestoreRefused(backup);
        }

        [Fact]
        public void A_backup_from_a_more_recent_version_is_refused()
        {
            var backup = _service.BackupTo(_usbDirectory);
            using (var connection = OpenPlain(backup))
            {
                connection.Execute("PRAGMA user_version = " + (_latestVersion + 1));
            }

            var error = AssertRestoreRefused(backup);

            Assert.Contains("version plus récente", error.Message);
        }

        [Fact]
        public void A_backup_of_the_current_version_is_accepted()
        {
            var backup = _service.BackupTo(_usbDirectory);

            _service.CheckRestorable(backup);
        }

        [Fact]
        public void A_database_that_is_not_a_Stokbox_one_is_refused()
        {
            Directory.CreateDirectory(_usbDirectory);
            var other = Path.Combine(_usbDirectory, "autre.db");
            using (var connection = OpenPlain(other))
            {
                connection.Execute("CREATE TABLE notes (id INTEGER PRIMARY KEY, text TEXT); PRAGMA user_version = 1;");
            }

            AssertRestoreRefused(other);
        }

        [Fact]
        public void A_missing_file_and_the_live_database_itself_are_refused()
        {
            AssertRestoreRefused(Path.Combine(_usbDirectory, "absent.db"));
            AssertRestoreRefused(_database.FilePath);
        }

        // A refused restoration changes nothing: no data replaced, and not even a safety backup taken.
        private BusinessRuleException AssertRestoreRefused(string filePath)
        {
            var before = Categories();

            Assert.Throws<BusinessRuleException>(() => _service.CheckRestorable(filePath));
            var error = Assert.Throws<BusinessRuleException>(() => _service.Restore(filePath));

            Assert.Equal(before, Categories());
            Assert.False(Directory.Exists(_backupsDirectory));
            return error;
        }

        private DateTime NextSecond()
        {
            var now = _now;
            _now = _now.AddSeconds(1);
            return now;
        }

        private void AddCategory(string name)
        {
            new CategoryRepository(_database.ConnectionFactory).Insert(name);
        }

        private string[] Categories()
        {
            return new CategoryRepository(_database.ConnectionFactory).GetAll().Select(c => c.Name).ToArray();
        }

        private static string[] CategoriesIn(string filePath)
        {
            using (var connection = OpenPlain(filePath))
            {
                return connection.Query<string>("SELECT name FROM categories ORDER BY name").ToArray();
            }
        }

        private static SQLiteConnection OpenPlain(string filePath)
        {
            var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = filePath, Pooling = false }.ToString());
            connection.Open();
            return connection;
        }
    }
}
