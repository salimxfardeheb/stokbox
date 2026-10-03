using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Stokbox.Core.Entities;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// One product of the label screen: ticked or not, and how many labels to print.
    /// </summary>
    public sealed class LabelSelectionRow : ObservableObject
    {
        public const int MaxCopies = 999;

        private Product _product;
        private bool _isSelected;
        private string _copiesText = "1";

        public LabelSelectionRow(Product product)
        {
            _product = product;
        }

        /// <summary>
        /// Raised when the tick or the number of copies changes.
        /// </summary>
        public event EventHandler SelectionChanged;

        public Product Product
        {
            get => _product;
            set => SetProperty(ref _product, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string CopiesText
        {
            get => _copiesText;
            set
            {
                if (SetProperty(ref _copiesText, value))
                {
                    OnPropertyChanged(nameof(HasCopiesError));
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Number of labels asked; null when the text is not an integer between 1 and 999.
        /// </summary>
        public int? Copies
        {
            get
            {
                return int.TryParse((CopiesText ?? string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var copies)
                    && copies >= 1
                    && copies <= MaxCopies
                        ? copies
                        : (int?)null;
            }
        }

        public bool HasCopiesError => Copies == null;
    }
}
