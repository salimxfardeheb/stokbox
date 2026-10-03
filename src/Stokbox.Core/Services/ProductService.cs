using System;
using System.Collections.Generic;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Validation;

namespace Stokbox.Core.Services
{
    public sealed class ProductService
    {
        public const string NameField = "Name";
        public const string CategoryField = "Category";
        public const string PurchasePriceField = "PurchasePrice";
        public const string SalePriceField = "SalePrice";

        /// <summary>
        /// Highest price accepted, in DA.
        /// </summary>
        public const decimal MaxPrice = 999999999.99m;

        private const string NotFoundMessage = "Ce produit n'existe plus.";

        private readonly IProductRepository _products;
        private readonly ICategoryRepository _categories;
        private readonly BarcodeGenerator _barcodeGenerator;

        public ProductService(IProductRepository products, ICategoryRepository categories, BarcodeGenerator barcodeGenerator)
        {
            _products = products ?? throw new ArgumentNullException(nameof(products));
            _categories = categories ?? throw new ArgumentNullException(nameof(categories));
            _barcodeGenerator = barcodeGenerator ?? throw new ArgumentNullException(nameof(barcodeGenerator));
        }

        /// <summary>
        /// Archived products are left out unless the criteria ask for them.
        /// </summary>
        public IReadOnlyList<Product> Search(ProductSearchCriteria criteria)
        {
            var source = criteria ?? new ProductSearchCriteria();
            var text = (source.Text ?? string.Empty).Trim();

            return _products.Search(new ProductSearchCriteria
            {
                Text = text.Length == 0 ? null : text,
                CategoryId = source.CategoryId,
                IncludeArchived = source.IncludeArchived,
                OutOfStockOnly = source.OutOfStockOnly
            });
        }

        public Product GetById(long id)
        {
            return _products.GetById(id);
        }

        /// <summary>
        /// The product carrying exactly this barcode, archived or not; null when there is none.
        /// </summary>
        public Product FindByBarcode(string barcode)
        {
            var code = (barcode ?? string.Empty).Trim();
            return code.Length == 0 ? null : _products.GetByBarcode(code);
        }

        /// <summary>
        /// At most one error per field; empty when the input is valid.
        /// </summary>
        public IReadOnlyList<ValidationError> Validate(ProductInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                errors.Add(new ValidationError(NameField, "La désignation est obligatoire."));
            }

            if (input.CategoryId == null)
            {
                errors.Add(new ValidationError(CategoryField, "La catégorie est obligatoire."));
            }
            else if (_categories.GetById(input.CategoryId.Value) == null)
            {
                errors.Add(new ValidationError(CategoryField, "Cette catégorie n'existe plus."));
            }

            var purchaseError = ValidatePurchasePrice(input.PurchasePrice);
            if (purchaseError != null)
            {
                errors.Add(new ValidationError(PurchasePriceField, purchaseError));
            }

            var saleError = ValidateSalePrice(input.SalePrice);
            if (saleError != null)
            {
                errors.Add(new ValidationError(SalePriceField, saleError));
            }

            return errors;
        }

        /// <summary>
        /// Creates the product with a newly generated barcode.
        /// </summary>
        public Product Create(ProductInput input)
        {
            EnsureValid(input);

            var id = _products.Insert(
                _barcodeGenerator.Next(),
                input.Name.Trim(),
                input.CategoryId.Value,
                Money.ToCents(input.PurchasePrice.Value),
                Money.ToCents(input.SalePrice.Value),
                DateTime.UtcNow);

            return _products.GetById(id);
        }

        /// <summary>
        /// Modifies the name, the category and the prices. The barcode cannot be changed.
        /// </summary>
        public Product Update(long id, ProductInput input)
        {
            EnsureValid(input);

            var updated = _products.Update(
                id,
                input.Name.Trim(),
                input.CategoryId.Value,
                Money.ToCents(input.PurchasePrice.Value),
                Money.ToCents(input.SalePrice.Value));
            if (!updated)
            {
                throw new BusinessRuleException(NotFoundMessage);
            }

            return _products.GetById(id);
        }

        public void Archive(long id)
        {
            SetArchived(id, true);
        }

        public void Unarchive(long id)
        {
            SetArchived(id, false);
        }

        private void SetArchived(long id, bool isArchived)
        {
            if (!_products.SetArchived(id, isArchived))
            {
                throw new BusinessRuleException(NotFoundMessage);
            }
        }

        private void EnsureValid(ProductInput input)
        {
            var errors = Validate(input);
            if (errors.Count > 0)
            {
                throw new ValidationException(errors);
            }
        }

        private static string ValidatePurchasePrice(decimal? price)
        {
            if (price == null)
            {
                return "Le prix d'achat est obligatoire.";
            }

            if (price.Value < 0)
            {
                return "Le prix d'achat ne peut pas être négatif.";
            }

            return ValidatePrecisionAndRange(price.Value);
        }

        private static string ValidateSalePrice(decimal? price)
        {
            if (price == null)
            {
                return "Le prix de vente est obligatoire.";
            }

            if (price.Value <= 0)
            {
                return "Le prix de vente doit être supérieur à 0.";
            }

            return ValidatePrecisionAndRange(price.Value);
        }

        private static string ValidatePrecisionAndRange(decimal price)
        {
            if (!Money.HasAtMostTwoDecimals(price))
            {
                return "Le prix ne peut pas avoir plus de 2 décimales.";
            }

            if (price > MaxPrice)
            {
                return "Le prix est trop élevé (maximum : 999 999 999,99 DA).";
            }

            return null;
        }
    }
}
