namespace Stokbox.Core.Services
{
    /// <summary>
    /// Brings the database schema up to the version expected by the application.
    /// </summary>
    public interface IDatabaseMigrator
    {
        /// <summary>
        /// Applies every pending migration, in order.
        /// </summary>
        /// <returns>The number of migrations applied (0 when the database is already up to date).</returns>
        int MigrateToLatest();
    }
}
