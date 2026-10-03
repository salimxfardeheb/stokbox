using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.Core;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Creation (no product given) or modification of a product.
    /// </summary>
    public sealed class ProductFormViewModel : ObservableObject
    {
        private const string InvalidAmountMessage = "Montant invalide. Exemples : 1250 ou 1250,50.";

        private readonly ProductService _productService;
        private readonly long? _productId;

        private string _name;
        private Category _selectedCategory;
        private string _purchasePriceText;
        private string _salePriceText;
        private string _nameError;
        private string _categoryError;
        private string _purchasePriceError;
        private string _salePriceError;
        private string _generalError;

        public ProductFormViewModel(ProductService productService, IReadOnlyList<Category> categories, Product product)
        {
            _productService = productService;
            Categories = categories;

            if (product == null)
            {
                Title = "Nouveau produit";
                BarcodeText = "Généré automatiquement à l'enregistrement";
                _name = string.Empty;
                _purchasePriceText = string.Empty;
                _salePriceText = string.Empty;
                _selectedCategory = categories.Count == 1 ? categories[0] : null;
            }
            else
            {
                _productId = product.Id;
                Title = "Modifier le produit";
                BarcodeText = product.Barcode;
                _name = product.Name;
                _purchasePriceText = Money.FormatForInput(product.PurchasePriceCents);
                _salePriceText = Money.FormatForInput(product.SalePriceCents);
                _selectedCategory = categories.FirstOrDefault(c => c.Id == product.CategoryId);
            }

            SaveCommand = new RelayCommand(Save);
        }

        /// <summary>
        /// Raised once the product is saved: the window closes.
        /// </summary>
        public event EventHandler Saved;

        public string Title { get; }

        /// <summary>
        /// Shown read-only: the barcode is never typed nor modified.
        /// </summary>
        public string BarcodeText { get; }

        public IReadOnlyList<Category> Categories { get; }

        public RelayCommand SaveCommand { get; }

        public long SavedProductId { get; private set; }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public Category SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        public string PurchasePriceText
        {
            get => _purchasePriceText;
            set => SetProperty(ref _purchasePriceText, value);
        }

        public string SalePriceText
        {
            get => _salePriceText;
            set => SetProperty(ref _salePriceText, value);
        }

        public string NameError
        {
            get => _nameError;
            private set => SetProperty(ref _nameError, value);
        }

        public string CategoryError
        {
            get => _categoryError;
            private set => SetProperty(ref _categoryError, value);
        }

        public string PurchasePriceError
        {
            get => _purchasePriceError;
            private set => SetProperty(ref _purchasePriceError, value);
        }

        public string SalePriceError
        {
            get => _salePriceError;
            private set => SetProperty(ref _salePriceError, value);
        }

        public string GeneralError
        {
            get => _generalError;
            private set => SetProperty(ref _generalError, value);
        }

        private void Save()
        {
            var errors = new Dictionary<string, string>();
            var input = new ProductInput
            {
                Name = Name,
                CategoryId = SelectedCategory?.Id,
                PurchasePrice = ReadPrice(PurchasePriceText, ProductService.PurchasePriceField, errors),
                SalePrice = ReadPrice(SalePriceText, ProductService.SalePriceField, errors)
            };

            // A price that could not be read keeps its own message rather than "obligatoire".
            AddErrors(errors, _productService.Validate(input));
            ShowErrors(errors, null);
            if (errors.Count > 0)
            {
                return;
            }

            try
            {
                var saved = _productId == null
                    ? _productService.Create(input)
                    : _productService.Update(_productId.Value, input);
                SavedProductId = saved.Id;
            }
            catch (ValidationException ex)
            {
                AddErrors(errors, ex.Errors);
                ShowErrors(errors, null);
                return;
            }
            catch (BusinessRuleException ex)
            {
                ShowErrors(errors, ex.Message);
                return;
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }

        private static decimal? ReadPrice(string text, string field, IDictionary<string, string> errors)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (Money.TryParse(text, out var amount))
            {
                return amount;
            }

            errors[field] = InvalidAmountMessage;
            return null;
        }

        private static void AddErrors(IDictionary<string, string> errors, IEnumerable<ValidationError> found)
        {
            foreach (var error in found)
            {
                if (!errors.ContainsKey(error.Field))
                {
                    errors[error.Field] = error.Message;
                }
            }
        }

        private void ShowErrors(IDictionary<string, string> errors, string generalError)
        {
            NameError = MessageFor(errors, ProductService.NameField);
            CategoryError = MessageFor(errors, ProductService.CategoryField);
            PurchasePriceError = MessageFor(errors, ProductService.PurchasePriceField);
            SalePriceError = MessageFor(errors, ProductService.SalePriceField);
            GeneralError = generalError;
        }

        private static string MessageFor(IDictionary<string, string> errors, string field)
        {
            return errors.TryGetValue(field, out var message) ? message : null;
        }
    }
}
