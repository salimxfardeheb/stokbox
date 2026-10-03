using System;
using System.Data.SQLite;
using System.Globalization;
using Dapper;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Validation;

namespace Stokbox.Data.Repositories
{
    public sealed class SaleRepository : ISaleRepository
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SaleRepository(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public Sale Create(NewSale sale)
        {
            if (sale == null)
            {
                throw new ArgumentNullException(nameof(sale));
            }

            using (var connection = _connectionFactory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                // Disposing the transaction without Commit rolls everything back: any exception below leaves no trace (RG-07).
                foreach (var line in sale.Lines)
                {
                    EnsureSellable(connection, transaction, line);
                }

                var number = NextNumber(connection, transaction, sale.NumberDate);
                var createdAt = SqliteDates.ToText(sale.CreatedAtUtc);

                var saleId = connection.ExecuteScalar<long>(
                    "INSERT INTO sales (number, created_at, total_cents, received_cents, change_cents, status) " +
                    "VALUES (@Number, @CreatedAt, @TotalCents, @ReceivedCents, @ChangeCents, @Status); " +
                    "SELECT last_insert_rowid();",
                    new
                    {
                        Number = number,
                        CreatedAt = createdAt,
                        sale.TotalCents,
                        sale.ReceivedCents,
                        sale.ChangeCents,
                        Status = Sale.StatusValidated
                    },
                    transaction);

                foreach (var line in sale.Lines)
                {
                    connection.Execute(
                        "INSERT INTO sale_lines (sale_id, product_id, quantity, unit_price_cents, unit_purchase_price_cents, line_total_cents) " +
                        "VALUES (@SaleId, @ProductId, @Quantity, @UnitPriceCents, @UnitPurchasePriceCents, @LineTotalCents)",
                        new
                        {
                            SaleId = saleId,
                            line.ProductId,
                            line.Quantity,
                            line.UnitPriceCents,
                            line.UnitPurchasePriceCents,
                            line.LineTotalCents
                        },
                        transaction);
                }

                // A sale takes out of the stock: the movement quantity is negative (RG-01).
                foreach (var line in sale.Lines)
                {
                    connection.Execute(
                        "INSERT INTO stock_movements (product_id, type, quantity, sale_id, created_at) " +
                        "VALUES (@ProductId, 'VENTE', @Quantity, @SaleId, @CreatedAt)",
                        new { line.ProductId, Quantity = -line.Quantity, SaleId = saleId, CreatedAt = createdAt },
                        transaction);
                }

                transaction.Commit();

                return new Sale
                {
                    Id = saleId,
                    Number = number,
                    CreatedAtUtc = sale.CreatedAtUtc,
                    TotalCents = sale.TotalCents,
                    ReceivedCents = sale.ReceivedCents,
                    ChangeCents = sale.ChangeCents,
                    Status = Sale.StatusValidated,
                    Lines = sale.Lines
                };
            }
        }

        // The cart was checked when it was filled; the stock may have changed since. The transaction holds
        // the write lock from its start, so what is read here still holds when the movements are inserted.
        private static void EnsureSellable(SQLiteConnection connection, SQLiteTransaction transaction, SaleLine line)
        {
            var product = connection.QuerySingleOrDefault<ProductState>(
                "SELECT p.name AS Name, p.is_archived AS IsArchived, " +
                "COALESCE((SELECT SUM(m.quantity) FROM stock_movements m WHERE m.product_id = p.id), 0) AS StockQuantity " +
                "FROM products p WHERE p.id = @ProductId",
                new { line.ProductId },
                transaction);

            if (product == null)
            {
                throw new BusinessRuleException("Le produit « " + line.ProductName + " » n'existe plus.");
            }

            if (product.IsArchived)
            {
                throw new BusinessRuleException("Le produit « " + product.Name + " » est archivé : il ne peut pas être vendu.");
            }

            if (line.Quantity < 1 || line.Quantity > product.StockQuantity)
            {
                throw new InsufficientStockException(product.Name, product.StockQuantity);
            }
        }

        // "V-AAAAMMJJ-0001": the sequence starts again at 1 each day.
        private static string NextNumber(SQLiteConnection connection, SQLiteTransaction transaction, DateTime numberDate)
        {
            var prefix = "V-" + numberDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-";

            var next = connection.ExecuteScalar<long>(
                "SELECT COALESCE(MAX(CAST(substr(number, @SequenceStart) AS INTEGER)), 0) + 1 " +
                "FROM sales WHERE substr(number, 1, @PrefixLength) = @Prefix",
                new { Prefix = prefix, PrefixLength = prefix.Length, SequenceStart = prefix.Length + 1 },
                transaction);

            return prefix + next.ToString("D4", CultureInfo.InvariantCulture);
        }

        private sealed class ProductState
        {
            public string Name { get; set; }

            public bool IsArchived { get; set; }

            public long StockQuantity { get; set; }
        }
    }
}
