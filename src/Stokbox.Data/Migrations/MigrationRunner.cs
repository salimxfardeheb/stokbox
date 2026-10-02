using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using Dapper;
using Stokbox.Core.Services;

namespace Stokbox.Data.Migrations
{
    /// <summary>
    /// Applies the pending migrations in order, each in its own transaction.
    /// The schema version is kept in PRAGMA user_version.
    /// </summary>
    public sealed class MigrationRunner : IDatabaseMigrator
    {
        private readonly SqliteConnectionFactory _connectionFactory;
        private readonly IReadOnlyList<Migration> _migrations;

        public MigrationRunner(SqliteConnectionFactory connectionFactory)
            : this(connectionFactory, EmbeddedMigrations.Load())
        {
        }

        public MigrationRunner(SqliteConnectionFactory connectionFactory, IReadOnlyList<Migration> migrations)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            if (migrations == null)
            {
                throw new ArgumentNullException(nameof(migrations));
            }

            _migrations = migrations.OrderBy(m => m.Version).ToList();
            EnsureContiguous(_migrations);
        }

        public int LatestVersion => _migrations.Count;

        public int MigrateToLatest()
        {
            using (var connection = _connectionFactory.Open())
            {
                var currentVersion = ReadVersion(connection);
                if (currentVersion > LatestVersion)
                {
                    throw new MigrationException(string.Format(
                        CultureInfo.InvariantCulture,
                        "La base de données (version {0}) a été créée par une version plus récente de Stokbox (version attendue : {1}).",
                        currentVersion,
                        LatestVersion));
                }

                var applied = 0;
                foreach (var migration in _migrations.Where(m => m.Version > currentVersion))
                {
                    Apply(connection, migration);
                    applied++;
                }

                return applied;
            }
        }

        private static void Apply(SQLiteConnection connection, Migration migration)
        {
            // Disposing an uncommitted transaction rolls it back: a failing script leaves no trace.
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    connection.Execute(migration.Sql, transaction: transaction);

                    // PRAGMA does not accept bound parameters; the value is an int, never user input.
                    connection.Execute(
                        "PRAGMA user_version = " + migration.Version.ToString(CultureInfo.InvariantCulture),
                        transaction: transaction);

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    throw new MigrationException(string.Format(
                        CultureInfo.InvariantCulture,
                        "Échec de la migration {0:0000}_{1} de la base de données.",
                        migration.Version,
                        migration.Name), ex);
                }
            }
        }

        private static int ReadVersion(SQLiteConnection connection)
        {
            return connection.ExecuteScalar<int>("PRAGMA user_version");
        }

        private static void EnsureContiguous(IReadOnlyList<Migration> migrations)
        {
            for (var i = 0; i < migrations.Count; i++)
            {
                if (migrations[i].Version != i + 1)
                {
                    throw new MigrationException(string.Format(
                        CultureInfo.InvariantCulture,
                        "Les migrations doivent être numérotées de 1 à {0} sans trou ni doublon (trouvé : {1}).",
                        migrations.Count,
                        string.Join(", ", migrations.Select(m => m.Version))));
                }
            }
        }
    }
}
