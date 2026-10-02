using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using Stokbox.Data.Migrations;
using Xunit;

namespace Stokbox.Data.Tests
{
    public class MigrationRunnerTests : IDisposable
    {
        // Bump when a new migration script is added.
        private const int LatestVersion = 1;

        private static readonly string[] ExpectedTables =
        {
            "barcode_sequence",
            "categories",
            "products",
            "return_lines",
            "returns",
            "sale_lines",
            "sales",
            "settings",
            "stock_movements"
        };

        private readonly TempDatabase _database = new TempDatabase();

        public void Dispose()
        {
            _database.Dispose();
        }

        [Fact]
        public void Empty_database_is_migrated_to_the_latest_version_with_all_tables()
        {
            var applied = new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();

            Assert.Equal(LatestVersion, applied);
            using (var connection = _database.Open())
            {
                Assert.Equal(LatestVersion, UserVersion(connection));
                Assert.Equal(ExpectedTables, TableNames(connection));
            }
        }

        [Fact]
        public void Migrating_an_up_to_date_database_changes_nothing()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();
            IReadOnlyList<string> schemaBefore;
            using (var connection = _database.Open())
            {
                connection.Execute("UPDATE barcode_sequence SET last_value = 42 WHERE id = 1");
                schemaBefore = Schema(connection);
            }

            var applied = new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();

            Assert.Equal(0, applied);
            using (var connection = _database.Open())
            {
                Assert.Equal(LatestVersion, UserVersion(connection));
                Assert.Equal(schemaBefore, Schema(connection));
                Assert.Equal(42, connection.ExecuteScalar<int>("SELECT last_value FROM barcode_sequence"));
                Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM barcode_sequence"));
            }
        }

        [Fact]
        public void Embedded_scripts_are_numbered_from_1_without_gap()
        {
            var migrations = EmbeddedMigrations.Load();

            Assert.Equal(Enumerable.Range(1, LatestVersion), migrations.Select(m => m.Version));
            Assert.Equal("initial", migrations[0].Name);
        }

        [Fact]
        public void Initial_schema_has_the_expected_indexes()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();

            using (var connection = _database.Open())
            {
                Assert.Equal(new[] { "barcode" }, IndexColumns(connection, "ix_products_barcode"));
                Assert.Equal(new[] { "name" }, IndexColumns(connection, "ix_products_name"));
                Assert.Equal(new[] { "product_id" }, IndexColumns(connection, "ix_stock_movements_product_id"));
                Assert.Equal(new[] { "created_at" }, IndexColumns(connection, "ix_sales_created_at"));
            }
        }

        [Fact]
        public void Initial_schema_starts_the_barcode_sequence_at_zero_in_a_single_row()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();

            using (var connection = _database.Open())
            {
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT last_value FROM barcode_sequence WHERE id = 1"));
                Assert.Throws<SQLiteException>(
                    () => connection.Execute("INSERT INTO barcode_sequence (id, last_value) VALUES (2, 0)"));
            }
        }

        [Fact]
        public void Initial_schema_rejects_invalid_rows()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();

            using (var connection = _database.Open())
            {
                InsertProduct(connection, "2000000000015", salePriceCents: 15000);

                // Sale price must be > 0 (RG-09).
                Assert.Throws<SQLiteException>(() => InsertProduct(connection, "2000000000022", salePriceCents: 0));

                // Barcode is unique (RG-02).
                Assert.Throws<SQLiteException>(() => InsertProduct(connection, "2000000000015", salePriceCents: 15000));

                // Unknown category: foreign keys are enforced.
                Assert.Throws<SQLiteException>(
                    () => InsertProduct(connection, "2000000000039", salePriceCents: 15000, categoryId: 999));

                // Unknown movement type.
                Assert.Throws<SQLiteException>(() => connection.Execute(
                    "INSERT INTO stock_movements (product_id, type, quantity, created_at) " +
                    "VALUES (1, @Type, 1, @CreatedAt)",
                    new { Type = "AJUSTEMENT", CreatedAt = "2026-01-01T00:00:00Z" }));

                // Unknown sale status.
                Assert.Throws<SQLiteException>(() => connection.Execute(
                    "INSERT INTO sales (number, created_at, total_cents, received_cents, change_cents, status) " +
                    "VALUES (@Number, @CreatedAt, 0, 0, 0, @Status)",
                    new { Number = "V-1", CreatedAt = "2026-01-01T00:00:00Z", Status = "BROUILLON" }));
            }
        }

        [Fact]
        public void Migrations_are_applied_in_version_order_whatever_the_input_order()
        {
            var migrations = new[]
            {
                new Migration(2, "second", "INSERT INTO a (id) VALUES (1);"),
                new Migration(1, "first", "CREATE TABLE a (id INTEGER PRIMARY KEY);")
            };

            var applied = new MigrationRunner(_database.ConnectionFactory, migrations).MigrateToLatest();

            Assert.Equal(2, applied);
            using (var connection = _database.Open())
            {
                Assert.Equal(2, UserVersion(connection));
                Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM a"));
            }
        }

        [Fact]
        public void Only_pending_migrations_are_applied()
        {
            var first = new Migration(1, "first", "CREATE TABLE a (id INTEGER PRIMARY KEY);");
            var second = new Migration(2, "second", "CREATE TABLE b (id INTEGER PRIMARY KEY);");
            new MigrationRunner(_database.ConnectionFactory, new[] { first }).MigrateToLatest();

            var applied = new MigrationRunner(_database.ConnectionFactory, new[] { first, second }).MigrateToLatest();

            Assert.Equal(1, applied);
            using (var connection = _database.Open())
            {
                Assert.Equal(2, UserVersion(connection));
                Assert.Equal(new[] { "a", "b" }, TableNames(connection));
            }
        }

        [Fact]
        public void A_failing_migration_is_rolled_back_and_keeps_the_previous_ones()
        {
            var migrations = new[]
            {
                new Migration(1, "first", "CREATE TABLE a (id INTEGER PRIMARY KEY);"),
                new Migration(2, "broken", "CREATE TABLE b (id INTEGER PRIMARY KEY); INSERT INTO missing (id) VALUES (1);"),
                new Migration(3, "third", "CREATE TABLE c (id INTEGER PRIMARY KEY);")
            };

            var error = Assert.Throws<MigrationException>(
                () => new MigrationRunner(_database.ConnectionFactory, migrations).MigrateToLatest());

            Assert.Contains("0002_broken", error.Message);
            using (var connection = _database.Open())
            {
                Assert.Equal(1, UserVersion(connection));
                Assert.Equal(new[] { "a" }, TableNames(connection));
            }
        }

        [Fact]
        public void A_database_newer_than_the_application_is_refused()
        {
            using (var connection = _database.Open())
            {
                connection.Execute("PRAGMA user_version = 99");
            }

            Assert.Throws<MigrationException>(
                () => new MigrationRunner(_database.ConnectionFactory).MigrateToLatest());

            using (var connection = _database.Open())
            {
                Assert.Equal(99, UserVersion(connection));
                Assert.Empty(TableNames(connection));
            }
        }

        [Theory]
        [InlineData(2, 3)]
        [InlineData(1, 3)]
        [InlineData(1, 1)]
        public void Migration_numbers_must_run_from_1_without_gap_or_duplicate(int firstVersion, int secondVersion)
        {
            var migrations = new[]
            {
                new Migration(firstVersion, "first", "SELECT 1;"),
                new Migration(secondVersion, "second", "SELECT 1;")
            };

            Assert.Throws<MigrationException>(() => new MigrationRunner(_database.ConnectionFactory, migrations));
        }

        private static void InsertProduct(SQLiteConnection connection, string barcode, int salePriceCents, int? categoryId = null)
        {
            connection.Execute(
                "INSERT INTO products (barcode, name, category_id, purchase_price_cents, sale_price_cents, created_at) " +
                "VALUES (@Barcode, @Name, @CategoryId, @PurchasePriceCents, @SalePriceCents, @CreatedAt)",
                new
                {
                    Barcode = barcode,
                    Name = "Produit de test",
                    CategoryId = categoryId,
                    PurchasePriceCents = 10000,
                    SalePriceCents = salePriceCents,
                    CreatedAt = "2026-01-01T00:00:00Z"
                });
        }

        private static int UserVersion(SQLiteConnection connection)
        {
            return connection.ExecuteScalar<int>("PRAGMA user_version");
        }

        private static string[] TableNames(SQLiteConnection connection)
        {
            return connection
                .Query<string>("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name")
                .ToArray();
        }

        private static IReadOnlyList<string> Schema(SQLiteConnection connection)
        {
            return connection
                .Query<string>("SELECT type || ' ' || name || ' ' || IFNULL(sql, '') FROM sqlite_master ORDER BY type, name")
                .ToList();
        }

        private static string[] IndexColumns(SQLiteConnection connection, string indexName)
        {
            return connection
                .Query<string>("SELECT name FROM pragma_index_info(@IndexName) ORDER BY seqno", new { IndexName = indexName })
                .ToArray();
        }
    }
}
