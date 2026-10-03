using System.Collections.Generic;

namespace Stokbox.App.Services
{
    public interface IPrinterCatalog
    {
        /// <summary>
        /// Names of the printers installed in Windows, sorted; empty when they cannot be listed.
        /// </summary>
        IReadOnlyList<string> GetPrinterNames();
    }
}
