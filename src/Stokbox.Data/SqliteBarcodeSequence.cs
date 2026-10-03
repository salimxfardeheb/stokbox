using System;
using Dapper;
using Stokbox.Core.Repositories;

namespace Stokbox.Data
{
    public sealed class SqliteBarcodeSequence : IBarcodeSequence
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteBarcodeSequence(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public long Next()
        {
            using (var connection = _connectionFactory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                // The UPDATE takes the write lock first: two callers can never read the same value.
                connection.Execute(
                    "UPDATE barcode_sequence SET last_value = last_value + 1 WHERE id = 1",
                    transaction: transaction);
                var value = connection.ExecuteScalar<long>(
                    "SELECT last_value FROM barcode_sequence WHERE id = 1",
                    transaction: transaction);

                transaction.Commit();
                return value;
            }
        }
    }
}
