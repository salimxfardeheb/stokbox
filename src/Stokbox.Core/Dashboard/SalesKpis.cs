using System;

namespace Stokbox.Core.Dashboard
{
    /// <summary>
    /// Sales indicators of a period. Only validated sales count; a return is deducted in the period
    /// it was made in, at the prices frozen on the sale line (RG-04).
    /// </summary>
    public sealed class SalesKpis
    {
        public SalesKpis(
            long saleCount,
            long grossRevenueCents,
            long netRevenueCents,
            long netCostCents,
            long netItemCount)
        {
            SaleCount = saleCount;
            GrossRevenueCents = grossRevenueCents;
            NetRevenueCents = netRevenueCents;
            NetCostCents = netCostCents;
            NetItemCount = netItemCount;
        }

        /// <summary>
        /// Number of validated sales of the period.
        /// </summary>
        public long SaleCount { get; }

        /// <summary>
        /// Total of the validated sales of the period, before returns.
        /// </summary>
        public long GrossRevenueCents { get; }

        public long NetRevenueCents { get; }

        public long NetCostCents { get; }

        public long GrossProfitCents => NetRevenueCents - NetCostCents;

        /// <summary>
        /// Gross profit relative to the net revenue, in percent with 1 decimal; null when the net revenue is 0.
        /// </summary>
        public decimal? MarginRatePercent
        {
            get
            {
                if (NetRevenueCents == 0)
                {
                    return null;
                }

                return Math.Round(GrossProfitCents * 100m / NetRevenueCents, 1, MidpointRounding.AwayFromZero);
            }
        }

        /// <summary>
        /// Total of the validated sales divided by their number, before returns; null when there is no sale.
        /// </summary>
        public long? AverageBasketCents
        {
            get
            {
                if (SaleCount == 0)
                {
                    return null;
                }

                return (long)Math.Round((decimal)GrossRevenueCents / SaleCount, 0, MidpointRounding.AwayFromZero);
            }
        }

        /// <summary>
        /// Quantities sold minus quantities returned over the period.
        /// </summary>
        public long NetItemCount { get; }
    }
}
