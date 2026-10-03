using System;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Validation;

namespace Stokbox.Core.Services
{
    public sealed class StockService
    {
        public const string QuantityField = "Quantity";

        /// <summary>
        /// Highest quantity accepted in one entry; stops a barcode scanned into the quantity field.
        /// </summary>
        public const int MaxEntryQuantity = 100000;

        private readonly IProductRepository _products;
        private readonly IStockMovementRepository _movements;

        public StockService(IProductRepository products, IStockMovementRepository movements)
        {
            _products = products ?? throw new ArgumentNullException(nameof(products));
            _movements = movements ?? throw new ArgumentNullException(nameof(movements));
        }

        /// <summary>
        /// Adds the quantity to the stock of an active product by recording an ENTREE movement (RG-01).
        /// </summary>
        public StockEntry AddEntry(long productId, int quantity)
        {
            if (quantity <= 0)
            {
                throw new ValidationException(QuantityField, "La quantité doit être un nombre entier supérieur à 0.");
            }

            if (quantity > MaxEntryQuantity)
            {
                throw new ValidationException(QuantityField, "La quantité est trop élevée (maximum : 100 000 par entrée).");
            }

            var product = _products.GetById(productId);
            if (product == null)
            {
                throw new BusinessRuleException("Ce produit n'existe plus.");
            }

            if (product.IsArchived)
            {
                throw new BusinessRuleException("Ce produit est archivé : aucune entrée de stock n'est possible.");
            }

            var createdAtUtc = DateTime.UtcNow;
            _movements.AddEntry(productId, quantity, createdAtUtc);

            return new StockEntry(product, quantity, createdAtUtc);
        }
    }
}
