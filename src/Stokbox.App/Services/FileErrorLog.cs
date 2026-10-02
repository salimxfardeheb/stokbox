using System;
using System.Globalization;
using System.IO;
using System.Text;
using Stokbox.Core.Services;

namespace Stokbox.App.Services
{
    /// <summary>
    /// Appends unexpected errors to one text file per day: logs\stokbox-yyyyMMdd.log.
    /// </summary>
    public sealed class FileErrorLog : IErrorLog
    {
        private readonly object _sync = new object();

        public FileErrorLog(string logsDirectory)
        {
            LogsDirectory = logsDirectory ?? throw new ArgumentNullException(nameof(logsDirectory));
        }

        public string LogsDirectory { get; }

        public void Write(string context, Exception exception)
        {
            try
            {
                var fileName = "stokbox-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log";
                var entry = new StringBuilder()
                    .Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))
                    .Append(" [").Append(context).AppendLine("]")
                    .AppendLine(exception == null ? "(exception inconnue)" : exception.ToString())
                    .AppendLine()
                    .ToString();

                lock (_sync)
                {
                    Directory.CreateDirectory(LogsDirectory);
                    File.AppendAllText(Path.Combine(LogsDirectory, fileName), entry, Encoding.UTF8);
                }
            }
            catch
            {
                // The journal is the last resort: a failure to write it must never hide the original error.
            }
        }
    }
}
