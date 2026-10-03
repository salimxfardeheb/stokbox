using System.Globalization;

namespace Stokbox.Core.Validation
{
    /// <summary>
    /// The quantity asked exceeds the stock available (RG-10).
    /// </summary>
    public sealed class InsufficientStockException : BusinessRuleException
    {
        public InsufficientStockException(long available)
            : base("Stock insuffisant : " + Format(available) + " disponible(s)")
        {
            Available = available;
        }

        public InsufficientStockException(string productName, long available)
            : base("Stock insuffisant pour « " + productName + " » : " + Format(available) + " disponible(s)")
        {
            Available = available;
        }

        public long Available { get; }

        // A stock already negative has nothing left to sell.
        private static string Format(long available)
        {
            return (available < 0 ? 0 : available).ToString(CultureInfo.InvariantCulture);
        }
    }
}
