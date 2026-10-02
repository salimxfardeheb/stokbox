using System;
using System.IO;

namespace Stokbox.Core
{
    /// <summary>
    /// Locations of the files Stokbox writes on the machine (database, logs).
    /// </summary>
    public sealed class AppPaths
    {
        public const string AppFolderName = "Stokbox";
        public const string DatabaseFileName = "stokbox.db";
        public const string LogsFolderName = "logs";

        public AppPaths(string dataDirectory)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory))
            {
                throw new ArgumentException("Le dossier de données est obligatoire.", nameof(dataDirectory));
            }

            DataDirectory = dataDirectory;
        }

        public string DataDirectory { get; }

        public string DatabaseFilePath => Path.Combine(DataDirectory, DatabaseFileName);

        public string LogsDirectory => Path.Combine(DataDirectory, LogsFolderName);

        /// <summary>
        /// Paths under %ProgramData%\Stokbox.
        /// </summary>
        public static AppPaths CreateDefault()
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return new AppPaths(Path.Combine(programData, AppFolderName));
        }
    }
}
