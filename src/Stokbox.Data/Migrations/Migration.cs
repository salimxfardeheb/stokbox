using System;

namespace Stokbox.Data.Migrations
{
    /// <summary>
    /// One numbered schema script. Version N brings the database from N-1 to N.
    /// </summary>
    public sealed class Migration
    {
        public Migration(int version, string name, string sql)
        {
            if (version < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Le numéro de migration doit être supérieur ou égal à 1.");
            }

            Version = version;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Sql = sql ?? throw new ArgumentNullException(nameof(sql));
        }

        public int Version { get; }

        public string Name { get; }

        public string Sql { get; }
    }
}
