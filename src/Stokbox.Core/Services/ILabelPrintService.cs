using Stokbox.Core.Entities;

namespace Stokbox.Core.Services
{
    public interface ILabelPrintService
    {
        /// <summary>
        /// Prints the given number of price labels of the product.
        /// </summary>
        void PrintLabels(Product product, int copies);
    }
}
