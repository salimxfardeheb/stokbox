using System;

namespace Stokbox.Core.Entities
{
    /// <summary>
    /// One sale as listed in the history.
    /// </summary>
    public sealed class SaleSummary
    {
        public long Id { get; set; }

        public string Number { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// Sum of the quantities sold.
        /// </summary>
        public int ArticleCount { get; set; }

        public long TotalCents { get; set; }

        public string Status { get; set; }
    }
}
