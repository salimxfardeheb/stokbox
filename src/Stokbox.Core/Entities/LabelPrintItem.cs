using System;

namespace Stokbox.Core.Entities
{
    /// <summary>
    /// A product and the number of labels to print for it.
    /// </summary>
    public sealed class LabelPrintItem
    {
        public LabelPrintItem(Product product, int copies)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
            if (copies <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(copies), "Au moins un exemplaire est attendu.");
            }

            Copies = copies;
        }

        public Product Product { get; }

        public int Copies { get; }
    }
}
