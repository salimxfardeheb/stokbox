using System;
using System.Collections.Generic;
using System.Linq;

namespace Stokbox.Core.Entities
{
    /// <summary>
    /// Articles of a sale brought back by the customer at one time.
    /// </summary>
    public sealed class SaleReturn
    {
        public long Id { get; set; }

        public long SaleId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public IReadOnlyList<SaleReturnLine> Lines { get; set; }

        /// <summary>
        /// Amount given back to the customer: quantities × prices frozen at the sale.
        /// </summary>
        public long RefundCents => Lines.Sum(line => line.AmountCents);
    }
}
