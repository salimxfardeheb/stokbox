using Stokbox.App.Converters;
using Stokbox.Core.Entities;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// One returned article in the detail of a sale.
    /// </summary>
    public sealed class HistoryReturnRow
    {
        public HistoryReturnRow(SaleReturn saleReturn, SaleReturnLine line)
        {
            DateText = LocalDateTimeConverter.Format(saleReturn.CreatedAtUtc);
            ProductName = line.ProductName;
            Quantity = line.Quantity;
            AmountCents = line.AmountCents;
        }

        public string DateText { get; }

        public string ProductName { get; }

        public int Quantity { get; }

        public long AmountCents { get; }
    }
}
