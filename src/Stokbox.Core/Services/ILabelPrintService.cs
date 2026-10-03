using System.Collections.Generic;
using Stokbox.Core.Entities;

namespace Stokbox.Core.Services
{
    public interface ILabelPrintService
    {
        /// <summary>
        /// Prints the price labels of the given products with the saved label settings.
        /// </summary>
        /// <returns>False when nothing was sent to the printer (cancelled or failed; the user has been told why).</returns>
        bool Print(IReadOnlyList<LabelPrintItem> items);

        /// <summary>
        /// Prints one sample label with the given settings, saved or not.
        /// </summary>
        /// <returns>False when nothing was sent to the printer.</returns>
        bool PrintTestLabel(LabelSettings settings);
    }
}
