using System;
using System.Linq;
using Dapper;
using Stokbox.Core.Services;
using Stokbox.Data.Migrations;
using Xunit;

namespace Stokbox.Data.Tests
{
    public class SqliteBarcodeSequenceTests : IDisposable
    {
        private readonly TempDatabase _database = new TempDatabase();

        public SqliteBarcodeSequenceTests()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        [Fact]
        public void Sequence_starts_at_1_and_is_persisted()
        {
            Assert.Equal(1, new SqliteBarcodeSequence(_database.ConnectionFactory).Next());
            Assert.Equal(2, new SqliteBarcodeSequence(_database.ConnectionFactory).Next());

            using (var connection = _database.Open())
            {
                Assert.Equal(2, connection.ExecuteScalar<long>("SELECT last_value FROM barcode_sequence WHERE id = 1"));
            }
        }

        [Fact]
        public void Generated_barcodes_are_unique_and_valid()
        {
            var generator = new BarcodeGenerator(new SqliteBarcodeSequence(_database.ConnectionFactory));

            var codes = Enumerable.Range(0, 50).Select(_ => generator.Next()).ToList();

            Assert.All(codes, code => Assert.True(BarcodeGenerator.IsValidEan13(code), code));
            Assert.Equal(50, codes.Distinct().Count());
        }
    }
}
