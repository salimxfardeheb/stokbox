using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using Stokbox.App.Services;

namespace Stokbox.App.Printing
{
    /// <summary>
    /// The printers known to Windows: local ones and the connections to shared ones.
    /// </summary>
    public sealed class WindowsPrinters : IPrinterCatalog
    {
        private static readonly EnumeratedPrintQueueTypes[] QueueTypes =
        {
            EnumeratedPrintQueueTypes.Local,
            EnumeratedPrintQueueTypes.Connections
        };

        public IReadOnlyList<string> GetPrinterNames()
        {
            try
            {
                return GetQueues()
                    .Select(queue => queue.FullName)
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception)
            {
                // Print spooler stopped or unreachable: the screen still opens, with no printer to choose.
                return new string[0];
            }
        }

        /// <summary>
        /// The print queue carrying this name; null when there is none.
        /// </summary>
        public static PrintQueue FindQueue(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return null;
            }

            return GetQueues().FirstOrDefault(
                queue => string.Equals(queue.FullName, printerName, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<PrintQueue> GetQueues()
        {
            return new LocalPrintServer().GetPrintQueues(QueueTypes);
        }
    }
}
