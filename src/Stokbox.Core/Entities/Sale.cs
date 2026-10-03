using System;
using System.Collections.Generic;

namespace Stokbox.Core.Entities
{
    /// <summary>
    /// A validated cash sale. Never modified nor deleted afterwards (RG-05).
    /// </summary>
    public sealed class Sale
    {
        public const string StatusValidated = "VALIDEE";
        public const string StatusCancelled = "ANNULEE";

        public long Id { get; set; }

        /// <summary>
        /// "V-AAAAMMJJ-0001": the local day of the sale and its rank in that day.
        /// </summary>
        public string Number { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public long TotalCents { get; set; }

        public long ReceivedCents { get; set; }

        public long ChangeCents { get; set; }

        public string Status { get; set; }

        public IReadOnlyList<SaleLine> Lines { get; set; }
    }
}
