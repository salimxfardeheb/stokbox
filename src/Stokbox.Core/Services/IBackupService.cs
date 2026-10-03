namespace Stokbox.Core.Services
{
    /// <summary>
    /// Backups of the database and their restoration. A backup is always a consistent copy made
    /// while the application runs, and is checked before being reported as done.
    /// </summary>
    public interface IBackupService
    {
        /// <summary>
        /// Folder of the automatic backups.
        /// </summary>
        string BackupsDirectory { get; }

        /// <summary>
        /// Writes a verified backup named stokbox_AAAAMMJJ_HHMMSS.db in the given folder.
        /// </summary>
        /// <returns>The path of the backup file.</returns>
        /// <exception cref="Validation.BusinessRuleException">The backup could not be written or is not sound; no file is left.</exception>
        string BackupTo(string directory);

        /// <summary>
        /// Writes a verified backup in the automatic folder and keeps only the most recent ones.
        /// </summary>
        /// <returns>The path of the backup file.</returns>
        string BackupAutomatically();

        /// <summary>
        /// Checks that the file is a sound Stokbox backup that this version of the application can read.
        /// </summary>
        /// <exception cref="Validation.BusinessRuleException">The file is missing, corrupted, not a Stokbox database, or more recent than the application.</exception>
        void CheckRestorable(string filePath);

        /// <summary>
        /// Replaces the current database by the backup, after checking it and after backing up the current database.
        /// The application must be restarted afterwards.
        /// </summary>
        /// <returns>The path of the backup made of the database that was replaced.</returns>
        string Restore(string filePath);
    }
}
