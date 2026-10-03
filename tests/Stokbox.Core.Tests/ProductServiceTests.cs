using System.Linq;
using Stokbox.Core.Repositories;
using Stokbox.Core.Services;
using Stokbox.Core.Tests.Fakes;
using Stokbox.Core.Validation;
using Xunit;

namespace Stokbox.Core.Tests
{
    public class ProductServiceTests
    {
        private readonly InMemoryProductRepository _products = new InMemoryProductRepository();
        private readonly InMemoryCategoryRepository _categories = new InMemoryCategoryRepository();
        private readonly ProductService _service;
        private readonly long _categoryId;

        public ProductServiceTests()
        {
            _service = new ProductService(_products, _categories, new BarcodeGenerator(new InMemoryBarcodeSequence()));
            _categoryId = _categories.Insert("Boissons");
        }

        [Fact]
        public void A_valid_product_has_no_validation_error()
        {
            Assert.Empty(_service.Validate(ValidInput()));
        }

        // Désignation obligatoire.

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Name_is_required(string name)
        {
            var input = ValidInput();
            input.Name = name;

            AssertSingleError(input, ProductService.NameField, "La désignation est obligatoire.");
        }

        [Fact]
        public void A_non_empty_name_is_accepted()
        {
            var input = ValidInput();
            input.Name = "A";

            AssertNoError(input, ProductService.NameField);
        }

        // Catégorie obligatoire.

        [Fact]
        public void Category_is_required()
        {
            var input = ValidInput();
            input.CategoryId = null;

            AssertSingleError(input, ProductService.CategoryField, "La catégorie est obligatoire.");
        }

        [Fact]
        public void Category_must_exist()
        {
            var input = ValidInput();
            input.CategoryId = 999;

            AssertSingleError(input, ProductService.CategoryField, "Cette catégorie n'existe plus.");
        }

        [Fact]
        public void An_existing_category_is_accepted()
        {
            var input = ValidInput();
            input.CategoryId = _categories.Insert("Épicerie");

            AssertNoError(input, ProductService.CategoryField);
        }

        // Prix de vente > 0 (RG-09).

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("-0.01")]
        public void Sale_price_must_be_greater_than_zero(string salePrice)
        {
            var input = ValidInput();
            input.SalePrice = decimal.Parse(salePrice, System.Globalization.CultureInfo.InvariantCulture);

            AssertSingleError(input, ProductService.SalePriceField, "Le prix de vente doit être supérieur à 0.");
        }

        [Fact]
        public void Sale_price_is_required()
        {
            var input = ValidInput();
            input.SalePrice = null;

            AssertSingleError(input, ProductService.SalePriceField, "Le prix de vente est obligatoire.");
        }

        [Fact]
        public void The_smallest_positive_sale_price_is_accepted()
        {
            var input = ValidInput();
            input.SalePrice = 0.01m;

            AssertNoError(input, ProductService.SalePriceField);
        }

        // Prix d'achat >= 0.

        [Fact]
        public void Purchase_price_cannot_be_negative()
        {
            var input = ValidInput();
            input.PurchasePrice = -0.01m;

            AssertSingleError(input, ProductService.PurchasePriceField, "Le prix d'achat ne peut pas être négatif.");
        }

        [Fact]
        public void Purchase_price_is_required()
        {
            var input = ValidInput();
            input.PurchasePrice = null;

            AssertSingleError(input, ProductService.PurchasePriceField, "Le prix d'achat est obligatoire.");
        }

        [Fact]
        public void A_purchase_price_of_zero_is_accepted()
        {
            var input = ValidInput();
            input.PurchasePrice = 0m;

            AssertNoError(input, ProductService.PurchasePriceField);
        }

        // 2 décimales max (RG-09).

        [Fact]
        public void Sale_price_cannot_have_more_than_two_decimals()
        {
            var input = ValidInput();
            input.SalePrice = 12.345m;

            AssertSingleError(input, ProductService.SalePriceField, "Le prix ne peut pas avoir plus de 2 décimales.");
        }

        [Fact]
        public void Purchase_price_cannot_have_more_than_two_decimals()
        {
            var input = ValidInput();
            input.PurchasePrice = 0.001m;

            AssertSingleError(input, ProductService.PurchasePriceField, "Le prix ne peut pas avoir plus de 2 décimales.");
        }

        [Fact]
        public void Prices_with_two_decimals_are_accepted_and_stored_in_centimes()
        {
            var input = ValidInput();
            input.PurchasePrice = 99.99m;
            input.SalePrice = 1250.50m;

            var product = _service.Create(input);

            Assert.Equal(9999, product.PurchasePriceCents);
            Assert.Equal(125050, product.SalePriceCents);
        }

        // Plafond.

        [Fact]
        public void Prices_above_the_maximum_are_refused()
        {
            var input = ValidInput();
            input.PurchasePrice = ProductService.MaxPrice + 0.01m;
            input.SalePrice = ProductService.MaxPrice + 0.01m;

            var errors = _service.Validate(input);

            Assert.Equal(
                new[] { ProductService.PurchasePriceField, ProductService.SalePriceField },
                errors.Select(e => e.Field));
        }

        [Fact]
        public void The_maximum_price_is_accepted()
        {
            var input = ValidInput();
            input.PurchasePrice = ProductService.MaxPrice;
            input.SalePrice = ProductService.MaxPrice;

            Assert.Empty(_service.Validate(input));
        }

        [Fact]
        public void Every_faulty_field_is_reported_at_once()
        {
            var errors = _service.Validate(new ProductInput());

            Assert.Equal(
                new[]
                {
                    ProductService.NameField,
                    ProductService.CategoryField,
                    ProductService.PurchasePriceField,
                    ProductService.SalePriceField
                },
                errors.Select(e => e.Field));
        }

        // Création.

        [Fact]
        public void Creating_a_product_generates_a_valid_internal_barcode()
        {
            var first = _service.Create(ValidInput());
            var second = _service.Create(ValidInput());

            Assert.True(BarcodeGenerator.IsValidEan13(first.Barcode));
            Assert.True(BarcodeGenerator.IsValidEan13(second.Barcode));
            Assert.StartsWith("20", first.Barcode);
            Assert.NotEqual(first.Barcode, second.Barcode);
        }

        [Fact]
        public void Creating_a_product_trims_its_name()
        {
            var input = ValidInput();
            input.Name = "  Eau minérale 1,5 L ";

            Assert.Equal("Eau minérale 1,5 L", _service.Create(input).Name);
        }

        [Fact]
        public void An_invalid_product_is_not_created()
        {
            var input = ValidInput();
            input.SalePrice = 0m;

            var error = Assert.Throws<ValidationException>(() => _service.Create(input));

            Assert.Equal(ProductService.SalePriceField, error.Errors.Single().Field);
            Assert.Equal(0, _products.Count);
        }

        // Modification.

        [Fact]
        public void Updating_a_product_changes_everything_but_its_barcode()
        {
            var created = _service.Create(ValidInput());
            var barcode = created.Barcode;
            var otherCategoryId = _categories.Insert("Épicerie");

            var updated = _service.Update(created.Id, new ProductInput
            {
                Name = "Jus d'orange",
                CategoryId = otherCategoryId,
                PurchasePrice = 80m,
                SalePrice = 120m
            });

            Assert.Equal(barcode, updated.Barcode);
            Assert.Equal("Jus d'orange", updated.Name);
            Assert.Equal(otherCategoryId, updated.CategoryId);
            Assert.Equal(8000, updated.PurchasePriceCents);
            Assert.Equal(12000, updated.SalePriceCents);
        }

        [Fact]
        public void An_invalid_update_is_refused_and_changes_nothing()
        {
            var created = _service.Create(ValidInput());
            var input = ValidInput();
            input.Name = " ";

            Assert.Throws<ValidationException>(() => _service.Update(created.Id, input));
            Assert.Equal("Eau minérale", _products.GetById(created.Id).Name);
        }

        [Fact]
        public void Updating_a_missing_product_is_refused()
        {
            Assert.Throws<BusinessRuleException>(() => _service.Update(99, ValidInput()));
        }

        // Archivage (RG-03).

        [Fact]
        public void A_product_is_archived_then_unarchived()
        {
            var product = _service.Create(ValidInput());

            _service.Archive(product.Id);
            Assert.True(_products.GetById(product.Id).IsArchived);

            _service.Unarchive(product.Id);
            Assert.False(_products.GetById(product.Id).IsArchived);
        }

        [Fact]
        public void Archiving_a_missing_product_is_refused()
        {
            Assert.Throws<BusinessRuleException>(() => _service.Archive(99));
            Assert.Throws<BusinessRuleException>(() => _service.Unarchive(99));
        }

        // Recherche.

        [Fact]
        public void Search_excludes_archived_products_by_default()
        {
            var kept = _service.Create(ValidInput());
            var archived = _service.Create(ValidInput());
            _service.Archive(archived.Id);

            Assert.Equal(new[] { kept.Id }, _service.Search(null).Select(p => p.Id));
            Assert.Equal(
                new[] { kept.Id, archived.Id },
                _service.Search(new ProductSearchCriteria { IncludeArchived = true }).Select(p => p.Id));
        }

        [Theory]
        [InlineData("  eau ", "eau")]
        [InlineData("   ", null)]
        [InlineData(null, null)]
        public void Search_text_is_trimmed_and_ignored_when_empty(string typed, string expected)
        {
            _service.Search(new ProductSearchCriteria { Text = typed, CategoryId = _categoryId });

            Assert.Equal(expected, _products.LastCriteria.Text);
            Assert.Equal(_categoryId, _products.LastCriteria.CategoryId);
        }

        private ProductInput ValidInput()
        {
            return new ProductInput
            {
                Name = "Eau minérale",
                CategoryId = _categoryId,
                PurchasePrice = 25m,
                SalePrice = 35.50m
            };
        }

        private void AssertSingleError(ProductInput input, string field, string message)
        {
            var error = Assert.Single(_service.Validate(input));
            Assert.Equal(field, error.Field);
            Assert.Equal(message, error.Message);
            Assert.Throws<ValidationException>(() => _service.Create(input));
        }

        private void AssertNoError(ProductInput input, string field)
        {
            Assert.DoesNotContain(_service.Validate(input), e => e.Field == field);
        }
    }
}
