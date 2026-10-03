using System;

namespace Stokbox.Core.Dashboard
{
    public sealed class DailySales
    {
        public DailySales(DateTime date, long netRevenueCents)
        {
            Date = date;
            NetRevenueCents = netRevenueCents;
        }

        /// <summary>
        /// Local day (midnight, no time part).
        /// </summary>
        public DateTime Date { get; }

        public long NetRevenueCents { get; }
    }
}
