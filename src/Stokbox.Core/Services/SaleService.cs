using System;
using System.Linq;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Validation;

namespace Stokbox.Core.Services
{
    /// <summary>
    /// Cash sale: fills a cart under the stock rules, then validates it.
    /// </summary>
    public sealed class SaleService
    {
        public const string QuantityField = "Quantity";
        public const string ReceivedField = "Received";

        private readonly IProductRepository _products;
        private readonly ISaleRepository _sales;
        private readonly Func<DateTime> _utcNow;

        public SaleService(IProductRepository products, ISaleRepository sales)
            : this(products, sales, () => DateTime.UtcNow)
        {
        }

        public SaleService(IProductRepository products, ISaleRepository sales, Func<DateTime> utcNow)
        {
            _products = products ?? throw new ArgumentNullException(nameof(products));
            _sales = sales ?? throw new ArgumentNullException(nameof(sales));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        /// <summary>
        /// Adds one unit of the product: a new line, or one more on its existing line.
        /// </summary>
        public CartLine AddProduct(Cart cart, long productId)
        {
            var line = cart.Find(productId);
            var product = GetSellableProduct(productId, (line?.Quantity ?? 0) + 1);

            if (line == null)
            {
                line = new CartLine(product, 1);
                cart.Add(line);
            }
            else
            {
                line.Quantity++;
                line.CopyDetailsFrom(product);
            }

            return line;
        }

        public void SetQuantity(Cart cart, long productId, int quantity)
        {
            var line = cart.Find(productId);
            if (line == null)
            {
                throw new BusinessRuleException("Cette ligne n'est plus dans le panier.");
            }

            if (quantity < 1)
            {
                throw new ValidationException(QuantityField, "La quantité doit être un nombre entier supérieur à 0.");
            }

            var product = GetSellableProduct(productId, quantity);
            line.Quantity = quantity;
            line.CopyDetailsFrom(product);
        }

        public void RemoveLine(Cart cart, long productId)
        {
            var line = cart.Find(productId);
            if (line != null)
            {
                cart.Remove(line);
            }
        }

        /// <summary>
        /// Brings the cart in line with the products as they are now: names and prices are updated,
        /// quantities are lowered to the stock, lines that can no longer be sold are removed.
        /// </summary>
        /// <returns>True when something changed.</returns>
        public bool Refresh(Cart cart)
        {
            var changed = false;

            foreach (var line in cart.Lines.ToList())
            {
                var product = _products.GetById(line.ProductId);
                if (product == null || product.IsArchived || product.StockQuantity < 1)
                {
                    cart.Remove(line);
                    changed = true;
                    continue;
                }

                if (line.Quantity > product.StockQuantity)
                {
                    line.Quantity = (int)product.StockQuantity;
                    changed = true;
                }

                if (line.Name != product.Name
                    || line.UnitPriceCents != product.SalePriceCents
                    || line.UnitPurchasePriceCents != product.PurchasePriceCents)
                {
                    line.CopyDetailsFrom(product);
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// Records the sale in one transaction (RG-07) with the prices of the cart frozen on its lines (RG-04),
        /// then empties the cart. The amount received must cover the total (RG-08).
        /// </summary>
        public Sale Validate(Cart cart, long receivedCents)
        {
            if (cart.IsEmpty)
            {
                throw new BusinessRuleException("Le panier est vide.");
            }

            var total = cart.TotalCents;
            if (receivedCents < total)
            {
                throw new ValidationException(
                    ReceivedField,
                    "Montant reçu insuffisant : il manque " + Money.Format(total - receivedCents) + ".");
            }

            var now = _utcNow();
            var sale = _sales.Create(new NewSale
            {
                CreatedAtUtc = now,
                NumberDate = now.ToLocalTime().Date,
                TotalCents = total,
                ReceivedCents = receivedCents,
                ChangeCents = receivedCents - total,
                Lines = cart.Lines
                    .Select(line => new SaleLine
                    {
                        ProductId = line.ProductId,
                        ProductName = line.Name,
                        Quantity = line.Quantity,
                        UnitPriceCents = line.UnitPriceCents,
                        UnitPurchasePriceCents = line.UnitPurchasePriceCents,
                        LineTotalCents = line.LineTotalCents
                    })
                    .ToList()
            });

            cart.Clear();
            return sale;
        }

        // RG-03: an archived product is not sold. RG-10: never more than the stock.
        private Product GetSellableProduct(long productId, int quantity)
        {
            var product = _products.GetById(productId);
            if (product == null)
            {
                throw new BusinessRuleException("Ce produit n'existe plus.");
            }

            if (product.IsArchived)
            {
                throw new BusinessRuleException("Le produit « " + product.Name + " » est archivé : il ne peut pas être vendu.");
            }

            if (quantity > product.StockQuantity)
            {
                throw new InsufficientStockException(product.StockQuantity);
            }

            return product;
        }
    }
}
