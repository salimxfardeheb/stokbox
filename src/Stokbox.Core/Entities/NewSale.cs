using System;
using System.Collections.Generic;

namespace Stokbox.Core.Entities
{
    /// <summary>
    /// A sale ready to be recorded: everything but its id and its number.
    /// </summary>
    public sealed class NewSale
    {
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// Local day of the sale: the one written in its number and the one whose sequence it takes.
        /// </summary>
        public DateTime NumberDate { get; set; }

        public long TotalCents { get; set; }

        public long ReceivedCents { get; set; }

        public long ChangeCents { get; set; }

        public IReadOnlyList<SaleLine> Lines { get; set; }
    }
}
