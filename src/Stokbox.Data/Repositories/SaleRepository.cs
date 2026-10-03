using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using Dapper;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Validation;

namespace Stokbox.Data.Repositories
{
    public sealed class SaleRepository : ISaleRepository
    {
        // More sales than this in one search means the period is too wide to be read on screen anyway.
        private const int MaxSearchResults = 1000;

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

                    // As stored (milliseconds): the sale read back later carries the very same date.
                    CreatedAtUtc = SqliteDates.FromText(createdAt),
                    TotalCents = sale.TotalCents,
                    ReceivedCents = sale.ReceivedCents,
                    ChangeCents = sale.ChangeCents,
                    Status = Sale.StatusValidated,
                    Lines = sale.Lines
                };
            }
        }

        public IReadOnlyList<SaleSummary> Search(DateTime fromUtc, DateTime toUtc, string numberText)
        {
            var number = string.IsNullOrWhiteSpace(numberText) ? null : numberText.Trim().ToUpperInvariant();

            using (var connection = _connectionFactory.Open())
            {
                return connection
                    .Query<SaleRow>(
                        "SELECT s.id AS Id, s.number AS Number, s.created_at AS CreatedAt, " +
                        "s.total_cents AS TotalCents, s.status AS Status, " +
                        "COALESCE((SELECT SUM(l.quantity) FROM sale_lines l WHERE l.sale_id = s.id), 0) AS ArticleCount " +
                        "FROM sales s " +
                        "WHERE (@Number IS NOT NULL AND instr(upper(s.number), @Number) > 0) " +
                        "OR (@Number IS NULL AND s.created_at >= @From AND s.created_at < @To) " +
                        "ORDER BY s.created_at DESC, s.id DESC " +
                        "LIMIT @Limit",
                        new
                        {
                            Number = number,
                            From = SqliteDates.ToText(fromUtc),
                            To = SqliteDates.ToText(toUtc),
                            Limit = MaxSearchResults
                        })
                    .Select(row => new SaleSummary
                    {
                        Id = row.Id,
                        Number = row.Number,
                        CreatedAtUtc = SqliteDates.FromText(row.CreatedAt),
                        ArticleCount = row.ArticleCount,
                        TotalCents = row.TotalCents,
                        Status = row.Status
                    })
                    .ToList();
            }
        }

        public Sale GetById(long saleId)
        {
            using (var connection = _connectionFactory.Open())
            {
                var row = connection.QuerySingleOrDefault<SaleRow>(
                    "SELECT id AS Id, number AS Number, created_at AS CreatedAt, total_cents AS TotalCents, " +
                    "received_cents AS ReceivedCents, change_cents AS ChangeCents, status AS Status " +
                    "FROM sales WHERE id = @SaleId",
                    new { SaleId = saleId });
                if (row == null)
                {
                    return null;
                }

                var lines = connection
                    .Query<SaleLine>(
                        "SELECT l.id AS Id, l.product_id AS ProductId, p.name AS ProductName, l.quantity AS Quantity, " +
                        "l.unit_price_cents AS UnitPriceCents, l.unit_purchase_price_cents AS UnitPurchasePriceCents, " +
                        "l.line_total_cents AS LineTotalCents, " +
                        "COALESCE((SELECT SUM(rl.quantity) FROM return_lines rl WHERE rl.sale_line_id = l.id), 0) AS ReturnedQuantity " +
                        "FROM sale_lines l JOIN products p ON p.id = l.product_id " +
                        "WHERE l.sale_id = @SaleId ORDER BY l.id",
                        new { SaleId = saleId })
                    .ToList();

                return new Sale
                {
                    Id = row.Id,
                    Number = row.Number,
                    CreatedAtUtc = SqliteDates.FromText(row.CreatedAt),
                    TotalCents = row.TotalCents,
                    ReceivedCents = row.ReceivedCents,
                    ChangeCents = row.ChangeCents,
                    Status = row.Status,
                    Lines = lines
                };
            }
        }

        public IReadOnlyList<SaleReturn> GetReturns(long saleId)
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection
                    .Query<ReturnLineRow>(
                        "SELECT r.id AS ReturnId, r.created_at AS CreatedAt, rl.sale_line_id AS SaleLineId, " +
                        "p.name AS ProductName, rl.quantity AS Quantity, l.unit_price_cents AS UnitPriceCents " +
                        "FROM returns r " +
                        "JOIN return_lines rl ON rl.return_id = r.id " +
                        "JOIN sale_lines l ON l.id = rl.sale_line_id " +
                        "JOIN products p ON p.id = l.product_id " +
                        "WHERE r.sale_id = @SaleId ORDER BY r.id, rl.id",
                        new { SaleId = saleId })
                    .GroupBy(row => row.ReturnId)
                    .Select(group => new SaleReturn
                    {
                        Id = group.Key,
                        SaleId = saleId,
                        CreatedAtUtc = SqliteDates.FromText(group.First().CreatedAt),
                        Lines = group
                            .Select(row => new SaleReturnLine
                            {
                                SaleLineId = row.SaleLineId,
                                ProductName = row.ProductName,
                                Quantity = row.Quantity,
                                UnitPriceCents = row.UnitPriceCents
                            })
                            .ToList()
                    })
                    .ToList();
            }
        }

        public void Cancel(long saleId, DateTime cancelledAtUtc)
        {
            using (var connection = _connectionFactory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                EnsureValidated(connection, transaction, saleId, "Cette vente est déjà annulée.");

                var returnCount = connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM returns WHERE sale_id = @SaleId",
                    new { SaleId = saleId },
                    transaction);
                if (returnCount > 0)
                {
                    throw new BusinessRuleException("Cette vente a déjà fait l'objet d'un retour : elle ne peut plus être annulée.");
                }

                // The status is the only thing a cancellation changes on the sale itself (RG-05).
                connection.Execute(
                    "UPDATE sales SET status = @Status WHERE id = @SaleId",
                    new { Status = Sale.StatusCancelled, SaleId = saleId },
                    transaction);

                // Everything sold comes back in stock: one positive movement per line (RG-01).
                connection.Execute(
                    "INSERT INTO stock_movements (product_id, type, quantity, sale_id, created_at) " +
                    "SELECT product_id, 'ANNULATION', quantity, sale_id, @CreatedAt FROM sale_lines " +
                    "WHERE sale_id = @SaleId ORDER BY id",
                    new { SaleId = saleId, CreatedAt = SqliteDates.ToText(cancelledAtUtc) },
                    transaction);

                transaction.Commit();
            }
        }

        public SaleReturn Return(long saleId, IReadOnlyList<ReturnRequestLine> lines, DateTime returnedAtUtc)
        {
            if (lines == null || lines.Count == 0)
            {
                throw new ArgumentException("Au moins une ligne à retourner est attendue.", nameof(lines));
            }

            using (var connection = _connectionFactory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                EnsureValidated(connection, transaction, saleId, "Cette vente est annulée : aucun retour n'est possible.");

                // Every quantity is checked before anything is written; the transaction holds the write lock
                // from its start, so the quantities already returned cannot change in between.
                var returnedLines = new List<ReturnedLine>();
                foreach (var requested in lines.GroupBy(line => line.SaleLineId))
                {
                    var quantity = requested.Sum(line => line.Quantity);
                    var state = connection.QuerySingleOrDefault<ReturnedLine>(
                        "SELECT l.id AS SaleLineId, l.product_id AS ProductId, p.name AS ProductName, " +
                        "l.quantity AS SoldQuantity, l.unit_price_cents AS UnitPriceCents, " +
                        "COALESCE((SELECT SUM(rl.quantity) FROM return_lines rl WHERE rl.sale_line_id = l.id), 0) AS AlreadyReturned " +
                        "FROM sale_lines l JOIN products p ON p.id = l.product_id " +
                        "WHERE l.id = @SaleLineId AND l.sale_id = @SaleId",
                        new { SaleLineId = requested.Key, SaleId = saleId },
                        transaction);

                    if (state == null)
                    {
                        throw new BusinessRuleException("Un article à retourner ne fait pas partie de cette vente.");
                    }

                    var returnable = state.SoldQuantity - state.AlreadyReturned;
                    if (quantity < 1 || quantity > returnable)
                    {
                        throw new BusinessRuleException(
                            "Retour impossible pour « " + state.ProductName + " » : "
                            + returnable.ToString(CultureInfo.InvariantCulture) + " retournable(s).");
                    }

                    state.Quantity = quantity;
                    returnedLines.Add(state);
                }

                var createdAt = SqliteDates.ToText(returnedAtUtc);
                var returnId = connection.ExecuteScalar<long>(
                    "INSERT INTO returns (sale_id, created_at) VALUES (@SaleId, @CreatedAt); SELECT last_insert_rowid();",
                    new { SaleId = saleId, CreatedAt = createdAt },
                    transaction);

                foreach (var line in returnedLines)
                {
                    connection.Execute(
                        "INSERT INTO return_lines (return_id, sale_line_id, quantity) VALUES (@ReturnId, @SaleLineId, @Quantity)",
                        new { ReturnId = returnId, line.SaleLineId, line.Quantity },
                        transaction);

                    // What comes back goes back in stock: a positive movement (RG-01).
                    connection.Execute(
                        "INSERT INTO stock_movements (product_id, type, quantity, sale_id, created_at) " +
                        "VALUES (@ProductId, 'RETOUR', @Quantity, @SaleId, @CreatedAt)",
                        new { line.ProductId, line.Quantity, SaleId = saleId, CreatedAt = createdAt },
                        transaction);
                }

                transaction.Commit();

                return new SaleReturn
                {
                    Id = returnId,
                    SaleId = saleId,
                    CreatedAtUtc = SqliteDates.FromText(createdAt),
                    Lines = returnedLines
                        .Select(line => new SaleReturnLine
                        {
                            SaleLineId = line.SaleLineId,
                            ProductName = line.ProductName,
                            Quantity = line.Quantity,
                            UnitPriceCents = line.UnitPriceCents
                        })
                        .ToList()
                };
            }
        }

        private static void EnsureValidated(SQLiteConnection connection, SQLiteTransaction transaction, long saleId, string cancelledMessage)
        {
            var status = connection.ExecuteScalar<string>(
                "SELECT status FROM sales WHERE id = @SaleId",
                new { SaleId = saleId },
                transaction);

            if (status == null)
            {
                throw new BusinessRuleException("Cette vente n'existe pas.");
            }

            if (status != Sale.StatusValidated)
            {
                throw new BusinessRuleException(cancelledMessage);
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

        private sealed class SaleRow
        {
            public long Id { get; set; }

            public string Number { get; set; }

            public string CreatedAt { get; set; }

            public long TotalCents { get; set; }

            public long ReceivedCents { get; set; }

            public long ChangeCents { get; set; }

            public string Status { get; set; }

            public int ArticleCount { get; set; }
        }

        private sealed class ReturnLineRow
        {
            public long ReturnId { get; set; }

            public string CreatedAt { get; set; }

            public long SaleLineId { get; set; }

            public string ProductName { get; set; }

            public int Quantity { get; set; }

            public long UnitPriceCents { get; set; }
        }

        private sealed class ReturnedLine
        {
            public long SaleLineId { get; set; }

            public long ProductId { get; set; }

            public string ProductName { get; set; }

            public int SoldQuantity { get; set; }

            public long UnitPriceCents { get; set; }

            public int AlreadyReturned { get; set; }

            public int Quantity { get; set; }
        }

        private sealed class ProductState
        {
            public string Name { get; set; }

            public bool IsArchived { get; set; }

            public long StockQuantity { get; set; }
        }
    }
}
