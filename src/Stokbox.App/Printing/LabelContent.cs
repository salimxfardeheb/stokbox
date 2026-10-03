using Stokbox.Core;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;

namespace Stokbox.App.Printing
{
    /// <summary>
    /// What is written on one price label.
    /// </summary>
    public sealed class LabelContent
    {
        public LabelContent(string name, string priceText, string barcode)
        {
            Name = name ?? string.Empty;
            PriceText = priceText ?? string.Empty;
            Barcode = barcode ?? string.Empty;
        }

        public string Name { get; }

        /// <summary>
        /// Sale price as displayed: "1 250,00 DA".
        /// </summary>
        public string PriceText { get; }

        public string Barcode { get; }

        public static LabelContent FromProduct(Product product)
        {
            return new LabelContent(product.Name, Money.Format(product.SalePriceCents), product.Barcode);
        }

        /// <summary>
        /// Label of the test print. Its barcode is valid but is the very last of the internal range,
        /// so scanning it never finds a real product.
        /// </summary>
        public static LabelContent CreateSample()
        {
            return new LabelContent(
                "Étiquette de test Stokbox, désignation sur deux lignes",
                Money.Format(125000),
                BarcodeGenerator.FromSequenceValue(BarcodeGenerator.MaxSequenceValue));
        }
    }
}
