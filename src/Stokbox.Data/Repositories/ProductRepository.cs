using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Stokbox.Core;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;

namespace Stokbox.Data.Repositories
{
    public sealed class ProductRepository : IProductRepository
    {
        // The stock quantity is always the sum of the movements (RG-01).
        private const string SelectSql =
            "SELECT p.id AS Id, p.barcode AS Barcode, p.name AS Name, " +
            "p.category_id AS CategoryId, c.name AS CategoryName, " +
            "p.purchase_price_cents AS PurchasePriceCents, p.sale_price_cents AS SalePriceCents, " +
            "p.is_archived AS IsArchived, " +
            "COALESCE((SELECT SUM(m.quantity) FROM stock_movements m WHERE m.product_id = p.id), 0) AS StockQuantity " +
            "FROM products p LEFT JOIN categories c ON c.id = p.category_id ";

        private readonly SqliteConnectionFactory _connectionFactory;

        public ProductRepository(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public Product GetById(long id)
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection.QuerySingleOrDefault<Product>(SelectSql + "WHERE p.id = @Id", new { Id = id });
            }
        }

        public Product GetByBarcode(string barcode)
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection.QuerySingleOrDefault<Product>(
                    SelectSql + "WHERE p.barcode = @Barcode",
                    new { Barcode = barcode });
            }
        }

        public IReadOnlyList<Product> Search(ProductSearchCriteria criteria)
        {
            if (criteria == null)
            {
                throw new ArgumentNullException(nameof(criteria));
            }

            var text = string.IsNullOrWhiteSpace(criteria.Text) ? null : criteria.Text.Trim();

            using (var connection = _connectionFactory.Open())
            {
                return connection
                    .Query<Product>(
                        SelectSql +
                        "WHERE (@Text IS NULL OR p.barcode = @Text OR instr(fold(p.name), @FoldedText) > 0) " +
                        "AND (@CategoryId IS NULL OR p.category_id = @CategoryId) " +
                        "AND (@IncludeArchived = 1 OR p.is_archived = 0) " +
                        "ORDER BY fold(p.name), p.id",
                        new
                        {
                            Text = text,
                            FoldedText = TextNormalizer.Fold(text),
                            criteria.CategoryId,
                            IncludeArchived = criteria.IncludeArchived ? 1 : 0
                        })
                    .ToList();
            }
        }

        public long Insert(string barcode, string name, long categoryId, long purchasePriceCents, long salePriceCents, DateTime createdAtUtc)
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection.ExecuteScalar<long>(
                    "INSERT INTO products (barcode, name, category_id, purchase_price_cents, sale_price_cents, is_archived, created_at) " +
                    "VALUES (@Barcode, @Name, @CategoryId, @PurchasePriceCents, @SalePriceCents, 0, @CreatedAt); " +
                    "SELECT last_insert_rowid();",
                    new
                    {
                        Barcode = barcode,
                        Name = name,
                        CategoryId = categoryId,
                        PurchasePriceCents = purchasePriceCents,
                        SalePriceCents = salePriceCents,
                        CreatedAt = SqliteDates.ToText(createdAtUtc)
                    });
            }
        }

        public bool Update(long id, string name, long categoryId, long purchasePriceCents, long salePriceCents)
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection.Execute(
                    "UPDATE products SET name = @Name, category_id = @CategoryId, " +
                    "purchase_price_cents = @PurchasePriceCents, sale_price_cents = @SalePriceCents " +
                    "WHERE id = @Id",
                    new
                    {
                        Id = id,
                        Name = name,
                        CategoryId = categoryId,
                        PurchasePriceCents = purchasePriceCents,
                        SalePriceCents = salePriceCents
                    }) > 0;
            }
        }

        public bool SetArchived(long id, bool isArchived)
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection.Execute(
                    "UPDATE products SET is_archived = @IsArchived WHERE id = @Id",
                    new { Id = id, IsArchived = isArchived ? 1 : 0 }) > 0;
            }
        }
    }
}
