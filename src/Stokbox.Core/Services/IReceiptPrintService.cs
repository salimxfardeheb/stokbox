using Stokbox.Core.Entities;

namespace Stokbox.Core.Services
{
    public interface IReceiptPrintService
    {
        /// <summary>
        /// False when no receipt printer is chosen in the settings: receipts are then never offered.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Prints the receipt of the sale on the receipt printer of the settings.
        /// </summary>
        /// <returns>False when nothing was sent to the printer (the user has been told why).</returns>
        bool Print(Sale sale);

        /// <summary>
        /// Prints a sample receipt with the given settings, saved or not.
        /// </summary>
        /// <returns>False when nothing was sent to the printer.</returns>
        bool PrintTest(ReceiptSettings settings);
    }
}
