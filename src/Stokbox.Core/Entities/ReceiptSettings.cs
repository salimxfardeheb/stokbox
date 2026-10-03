namespace Stokbox.Core.Entities
{
    /// <summary>
    /// What heads a receipt, and the printer it goes to.
    /// </summary>
    public sealed class ReceiptSettings
    {
        public string ShopName { get; set; }

        public string ShopAddress { get; set; }

        public string ShopPhone { get; set; }

        /// <summary>
        /// Windows name of the receipt printer; null when receipts are disabled.
        /// </summary>
        public string PrinterName { get; set; }
    }
}
