using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.App.Printing;
using Stokbox.App.Services;
using Stokbox.Core;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Services;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Labels screen: tick products, set the number of copies, check the preview, print.
    /// </summary>
    public sealed class LabelsViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly LabelSettingsService _settingsService;
        private readonly ILabelPrintService _labelPrintService;
        private readonly IDialogService _dialogs;

        // Every row ever shown, by product id: a ticked product stays ticked when the search hides it.
        private readonly Dictionary<long, LabelSelectionRow> _rowsByProductId = new Dictionary<long, LabelSelectionRow>();

        private string _searchText = string.Empty;
        private LabelSelectionRow _currentRow;
        private LabelSettings _settings;
        private IReadOnlyList<LabelContent> _previewLabels;
        private string _summaryText;
        private string _previewCaption;
        private string _statusMessage;

        public LabelsViewModel(
            ProductService productService,
            LabelSettingsService settingsService,
            ILabelPrintService labelPrintService,
            IDialogService dialogs)
        {
            _productService = productService;
            _settingsService = settingsService;
            _labelPrintService = labelPrintService;
            _dialogs = dialogs;

            Rows = new ObservableCollection<LabelSelectionRow>();
            PrintCommand = new RelayCommand(Print, () => SelectedRows().Any());
            ClearSelectionCommand = new RelayCommand(ClearSelection, () => SelectedRows().Any());
            ToggleCurrentRowCommand = new RelayCommand(ToggleCurrentRow);
        }

        /// <summary>
        /// Products matching the search.
        /// </summary>
        public ObservableCollection<LabelSelectionRow> Rows { get; }

        public RelayCommand PrintCommand { get; }

        public RelayCommand ClearSelectionCommand { get; }

        public RelayCommand ToggleCurrentRowCommand { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ReloadRows();
                }
            }
        }

        /// <summary>
        /// Row highlighted in the list; previewed when nothing is ticked.
        /// </summary>
        public LabelSelectionRow CurrentRow
        {
            get => _currentRow;
            set
            {
                if (SetProperty(ref _currentRow, value))
                {
                    UpdatePreview();
                }
            }
        }

        public LabelSettings Settings
        {
            get => _settings;
            private set => SetProperty(ref _settings, value);
        }

        /// <summary>
        /// Labels of the first page that would be printed.
        /// </summary>
        public IReadOnlyList<LabelContent> PreviewLabels
        {
            get => _previewLabels;
            private set => SetProperty(ref _previewLabels, value);
        }

        public string PreviewCaption
        {
            get => _previewCaption;
            private set => SetProperty(ref _previewCaption, value);
        }

        public string SummaryText
        {
            get => _summaryText;
            private set => SetProperty(ref _summaryText, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Reads the settings and the products again; called each time the screen is shown.
        /// </summary>
        public void Refresh()
        {
            Settings = _settingsService.Get();
            StatusMessage = null;

            // Ticked products may have been renamed, repriced or archived meanwhile.
            foreach (var row in SelectedRows().ToList())
            {
                var product = _productService.GetById(row.Product.Id);
                if (product == null || product.IsArchived)
                {
                    row.IsSelected = false;
                }
                else
                {
                    row.Product = product;
                }
            }

            ReloadRows();
        }

        private void ReloadRows()
        {
            var found = _productService.Search(new ProductSearchCriteria { Text = SearchText });

            Rows.Clear();
            foreach (var product in found)
            {
                if (_rowsByProductId.TryGetValue(product.Id, out var row))
                {
                    row.Product = product;
                }
                else
                {
                    row = new LabelSelectionRow(product);
                    row.SelectionChanged += (sender, e) => OnSelectionChanged();
                    _rowsByProductId[product.Id] = row;
                }

                Rows.Add(row);
            }

            OnSelectionChanged();
        }

        private IEnumerable<LabelSelectionRow> SelectedRows()
        {
            return _rowsByProductId.Values
                .Where(row => row.IsSelected)
                .OrderBy(row => TextNormalizer.Fold(row.Product.Name))
                .ThenBy(row => row.Product.Id);
        }

        private void OnSelectionChanged()
        {
            StatusMessage = null;
            PrintCommand.NotifyCanExecuteChanged();
            ClearSelectionCommand.NotifyCanExecuteChanged();
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (Settings == null)
            {
                return;
            }

            var selected = SelectedRows().ToList();

            // A number of copies being typed counts as 1 until it is valid.
            var labelCount = selected.Sum(row => row.Copies ?? 1);
            var labels = selected
                .SelectMany(row => Enumerable.Repeat(LabelContent.FromProduct(row.Product), row.Copies ?? 1))
                .Take(Settings.LabelsPerPage)
                .ToList();

            if (selected.Count == 0)
            {
                var shown = CurrentRow ?? Rows.FirstOrDefault();
                if (shown != null)
                {
                    labels.Add(LabelContent.FromProduct(shown.Product));
                }

                SummaryText = "Aucun produit coché.";
            }
            else
            {
                SummaryText = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} — {1} — {2}",
                    Plural(selected.Count, "produit coché", "produits cochés"),
                    Plural(labelCount, "étiquette", "étiquettes"),
                    Plural(Settings.CountPages(labelCount), "page", "pages"));
            }

            PreviewLabels = labels;
            PreviewCaption = string.Format(
                CultureInfo.InvariantCulture,
                "Aperçu de la première page — étiquette {0} × {1} mm, {2} par page",
                FormatMillimetres(Settings.WidthMm),
                FormatMillimetres(Settings.HeightMm),
                Settings.LabelsPerPage);
        }

        private void Print()
        {
            var selected = SelectedRows().ToList();
            if (selected.Count == 0)
            {
                return;
            }

            var invalid = selected.FirstOrDefault(row => row.Copies == null);
            if (invalid != null)
            {
                _dialogs.ShowWarning(
                    "Nombre d'exemplaires invalide pour « " + invalid.Product.Name + " » : saisissez un entier de 1 à 999.");
                return;
            }

            var items = selected.Select(row => new LabelPrintItem(row.Product, row.Copies.Value)).ToList();
            StatusMessage = _labelPrintService.Print(items)
                ? Plural(items.Sum(item => item.Copies), "étiquette envoyée", "étiquettes envoyées") + " à l'imprimante."
                : null;
        }

        private void ToggleCurrentRow()
        {
            if (CurrentRow != null)
            {
                CurrentRow.IsSelected = !CurrentRow.IsSelected;
            }
        }

        private void ClearSelection()
        {
            foreach (var row in SelectedRows().ToList())
            {
                row.IsSelected = false;
            }
        }

        private static string Plural(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count > 1 ? plural : singular);
        }

        private static string FormatMillimetres(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
        }
    }
}
