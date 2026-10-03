using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Services;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Single search field picking one active product: a scanned or typed barcode selects it directly,
    /// any other text opens a list of results by name. Shown by the ProductSearchBox control.
    /// </summary>
    public sealed class ProductSearchViewModel : ObservableObject
    {
        private const int MaxResults = 20;
        private const int MinLiveSearchLength = 2;

        private readonly ProductService _productService;

        private string _text = string.Empty;
        private Product _selectedResult;
        private bool _isResultListOpen;
        private string _message;

        public ProductSearchViewModel(ProductService productService)
        {
            _productService = productService;
            Results = new ObservableCollection<Product>();
        }

        /// <summary>
        /// Raised when the user has picked a product.
        /// </summary>
        public event EventHandler<Product> ProductSelected;

        /// <summary>
        /// Asks the control to take the keyboard focus and select its text, so that the next scan replaces it.
        /// </summary>
        public event EventHandler FocusRequested;

        public ObservableCollection<Product> Results { get; }

        public string Text
        {
            get => _text;
            set
            {
                if (SetProperty(ref _text, value))
                {
                    Message = null;
                    UpdateLiveResults();
                }
            }
        }

        public Product SelectedResult
        {
            get => _selectedResult;
            set => SetProperty(ref _selectedResult, value);
        }

        public bool IsResultListOpen
        {
            get => _isResultListOpen;
            private set => SetProperty(ref _isResultListOpen, value);
        }

        /// <summary>
        /// Why the search gave nothing; null otherwise.
        /// </summary>
        public string Message
        {
            get => _message;
            private set => SetProperty(ref _message, value);
        }

        /// <summary>
        /// Enter key, sent by the keyboard or by the scanner at the end of a barcode.
        /// </summary>
        public void Submit()
        {
            if (IsResultListOpen && SelectedResult != null)
            {
                Select(SelectedResult);
                return;
            }

            var text = (Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return;
            }

            var isNumeric = IsAllDigits(text);
            if (isNumeric)
            {
                var product = _productService.FindByBarcode(text);
                if (product != null && product.IsArchived)
                {
                    Fail("Le produit « " + product.Name + " » est archivé.");
                    return;
                }

                if (product != null)
                {
                    Select(product);
                    return;
                }
            }

            // Digits that are not a known barcode may still be part of a name ("1664").
            ShowResults(text);
            if (!IsResultListOpen)
            {
                Fail(isNumeric
                    ? "Aucun produit avec le code-barres " + text + "."
                    : "Aucun produit trouvé pour « " + text + " ».");
            }
        }

        /// <summary>
        /// Up and down arrows: moves the highlighted result.
        /// </summary>
        public void MoveSelection(int offset)
        {
            if (!IsResultListOpen || Results.Count == 0)
            {
                return;
            }

            var index = Results.IndexOf(SelectedResult) + offset;
            SelectedResult = Results[Math.Max(0, Math.Min(Results.Count - 1, index))];
        }

        public void CloseResults()
        {
            IsResultListOpen = false;
            Results.Clear();
            SelectedResult = null;
        }

        public void Clear()
        {
            Text = string.Empty;
            Message = null;
            CloseResults();
        }

        public void RequestFocus()
        {
            FocusRequested?.Invoke(this, EventArgs.Empty);
        }

        // While typing a name the list follows the text; digits wait for Enter, as a scan must not be searched halfway.
        private void UpdateLiveResults()
        {
            var text = (Text ?? string.Empty).Trim();
            if (text.Length >= MinLiveSearchLength && !IsAllDigits(text))
            {
                ShowResults(text);
            }
            else
            {
                CloseResults();
            }
        }

        private void ShowResults(string text)
        {
            var found = _productService
                .Search(new ProductSearchCriteria { Text = text })
                .Take(MaxResults)
                .ToList();

            Results.Clear();
            foreach (var product in found)
            {
                Results.Add(product);
            }

            SelectedResult = Results.FirstOrDefault();
            IsResultListOpen = Results.Count > 0;
        }

        private void Select(Product product)
        {
            Clear();
            ProductSelected?.Invoke(this, product);
        }

        private void Fail(string message)
        {
            CloseResults();
            Message = message;
            RequestFocus();
        }

        private static bool IsAllDigits(string text)
        {
            return text.All(c => c >= '0' && c <= '9');
        }
    }
}
