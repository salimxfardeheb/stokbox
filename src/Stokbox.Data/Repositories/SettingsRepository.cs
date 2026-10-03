using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Stokbox.Core.Repositories;

namespace Stokbox.Data.Repositories
{
    public sealed class SettingsRepository : ISettingsRepository
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SettingsRepository(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyDictionary<string, string> GetAll()
        {
            using (var connection = _connectionFactory.Open())
            {
                return connection
                    .Query<SettingRow>("SELECT key AS Key, value AS Value FROM settings")
                    .ToDictionary(row => row.Key, row => row.Value);
            }
        }

        public void Save(IReadOnlyDictionary<string, string> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            using (var connection = _connectionFactory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var pair in values)
                {
                    connection.Execute(
                        "INSERT OR REPLACE INTO settings (key, value) VALUES (@Key, @Value)",
                        new { pair.Key, pair.Value },
                        transaction);
                }

                transaction.Commit();
            }
        }

        private sealed class SettingRow
        {
            public string Key { get; set; }

            public string Value { get; set; }
        }
    }
}
