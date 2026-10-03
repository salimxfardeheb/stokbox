using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Validation;

namespace Stokbox.Data.Repositories
{
    public sealed class CategoryRepository : ICategoryRepository
    {
        private const string CountProductsSql = "SELECT COUNT(*) FROM products WHERE category_id = @Id";

        private readonly SqliteConnectionFactory _connectionFactory;

        public CategoryRepository(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyList<Category> GetAll()
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection
                    .Query<Category>("SELECT id AS Id, name AS Name FROM categories ORDER BY fold(name), id")
                    .ToList();
            }
        }

        public Category GetById(long id)
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection.QuerySingleOrDefault<Category>(
                    "SELECT id AS Id, name AS Name FROM categories WHERE id = @Id",
                    new { Id = id });
            }
        }

        public long Insert(string name)
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection.ExecuteScalar<long>(
                    "INSERT INTO categories (name) VALUES (@Name); SELECT last_insert_rowid();",
                    new { Name = name });
            }
        }

        public bool Rename(long id, string name)
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection.Execute(
                    "UPDATE categories SET name = @Name WHERE id = @Id",
                    new { Id = id, Name = name }) > 0;
            }
        }

        public int CountProducts(long id)
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection.ExecuteScalar<int>(CountProductsSql, new { Id = id });
            }
        }

        public bool Delete(long id)
        {
            using (var connection = _connectionFactory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                if (connection.ExecuteScalar<int>(CountProductsSql, new { Id = id }, transaction) > 0)
                {
                    throw new CategoryNotEmptyException();
                }

                var deleted = connection.Execute(
                    "DELETE FROM categories WHERE id = @Id",
                    new { Id = id },
                    transaction) > 0;

                transaction.Commit();
                return deleted;
            }
        }
    }
}
