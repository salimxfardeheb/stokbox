using System;
using Dapper;
using Stokbox.Core.Repositories;

namespace Stokbox.Data.Repositories
{
    public sealed class StockMovementRepository : IStockMovementRepository
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public StockMovementRepository(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public long AddEntry(long productId, int quantity, DateTime createdAtUtc)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "Une entrée de stock a une quantité positive.");
            }

            using (var connection = _connectionFactory.Open())
            {
                return connection.ExecuteScalar<long>(
                    "INSERT INTO stock_movements (product_id, type, quantity, created_at) " +
                    "VALUES (@ProductId, 'ENTREE', @Quantity, @CreatedAt); " +
                    "SELECT last_insert_rowid();",
                    new { ProductId = productId, Quantity = quantity, CreatedAt = SqliteDates.ToText(createdAtUtc) });
            }
        }
    }
}
