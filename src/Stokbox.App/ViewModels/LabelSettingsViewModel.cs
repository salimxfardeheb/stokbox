using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.App.Printing;
using Stokbox.App.Services;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Settings > Labels: size and arrangement of the labels, printer, test print.
    /// </summary>
    public sealed class LabelSettingsViewModel : ObservableObject
    {
        private const string InvalidNumberMessage = "Saisissez un nombre, par exemple 50 ou 33,9.";
        private const string InvalidIntegerMessage = "Saisissez un nombre entier, par exemple 3.";

        private readonly LabelSettingsService _settingsService;
        private readonly ILabelPrintService _labelPrintService;
        private readonly IPrinterCatalog _printers;

        private PrinterOption _selectedPrinter;
        private LabelSettings _previewSettings;
        private IReadOnlyList<LabelContent> _previewLabels;
        private string _statusMessage;
        private bool _isLoading;

        public LabelSettingsViewModel(
            LabelSettingsService settingsService,
            ILabelPrintService labelPrintService,
            IPrinterCatalog printers)
        {
            _settingsService = settingsService;
            _labelPrintService = labelPrintService;
            _printers = printers;

            Width = new FormField("Largeur de l'étiquette (mm)", OnInputChanged);
            Height = new FormField("Hauteur de l'étiquette (mm)", OnInputChanged);
            MarginTop = new FormField("Marge haute de la page (mm)", OnInputChanged);
            MarginLeft = new FormField("Marge gauche de la page (mm)", OnInputChanged);
            HorizontalGap = new FormField("Espacement entre colonnes (mm)", OnInputChanged);
            VerticalGap = new FormField("Espacement entre lignes (mm)", OnInputChanged);
            Columns = new FormField("Colonnes par page", OnInputChanged);
            Rows = new FormField("Lignes par page", OnInputChanged);

            PrinterOptions = new ObservableCollection<PrinterOption>();
            SaveCommand = new RelayCommand(Save);
            PrintTestCommand = new RelayCommand(PrintTest);
        }

        public FormField Width { get; }

        public FormField Height { get; }

        public FormField MarginTop { get; }

        public FormField MarginLeft { get; }

        public FormField HorizontalGap { get; }

        public FormField VerticalGap { get; }

        public FormField Columns { get; }

        public FormField Rows { get; }

        public ObservableCollection<PrinterOption> PrinterOptions { get; }

        public RelayCommand SaveCommand { get; }

        public RelayCommand PrintTestCommand { get; }

        public PrinterOption SelectedPrinter
        {
            get => _selectedPrinter;
            set
            {
                if (_isLoading)
                {
                    return;
                }

                if (SetProperty(ref _selectedPrinter, value))
                {
                    StatusMessage = null;
                }
            }
        }

        /// <summary>
        /// Settings shown by the preview: the last valid state of the form.
        /// </summary>
        public LabelSettings PreviewSettings
        {
            get => _previewSettings;
            private set => SetProperty(ref _previewSettings, value);
        }

        public IReadOnlyList<LabelContent> PreviewLabels
        {
            get => _previewLabels;
            private set => SetProperty(ref _previewLabels, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Fills the form with the saved settings; called each time the screen is shown.
        /// </summary>
        public void Load()
        {
            var settings = _settingsService.Get();

            _isLoading = true;
            try
            {
                Width.Text = FormatMillimetres(settings.WidthMm);
                Height.Text = FormatMillimetres(settings.HeightMm);
                MarginTop.Text = FormatMillimetres(settings.MarginTopMm);
                MarginLeft.Text = FormatMillimetres(settings.MarginLeftMm);
                HorizontalGap.Text = FormatMillimetres(settings.HorizontalGapMm);
                VerticalGap.Text = FormatMillimetres(settings.VerticalGapMm);
                Columns.Text = settings.Columns.ToString(CultureInfo.InvariantCulture);
                Rows.Text = settings.Rows.ToString(CultureInfo.InvariantCulture);

                PrinterOptions.Clear();
                PrinterOptions.Add(new PrinterOption(null, "(Demander à chaque impression)"));
                var installed = _printers.GetPrinterNames();
                foreach (var name in installed)
                {
                    PrinterOptions.Add(new PrinterOption(name, name));
                }

                // A saved printer that Windows no longer lists stays visible instead of silently vanishing.
                if (settings.PrinterName != null && !installed.Contains(settings.PrinterName, StringComparer.OrdinalIgnoreCase))
                {
                    PrinterOptions.Add(new PrinterOption(settings.PrinterName, settings.PrinterName + " (introuvable)"));
                }
            }
            finally
            {
                _isLoading = false;
            }

            _selectedPrinter = PrinterOptions.FirstOrDefault(
                    option => string.Equals(option.Name, settings.PrinterName, StringComparison.OrdinalIgnoreCase))
                ?? PrinterOptions[0];
            OnPropertyChanged(nameof(SelectedPrinter));

            ShowErrors(new Dictionary<string, string>());
            StatusMessage = null;
            ShowPreview(settings);
        }

        private void OnInputChanged()
        {
            if (_isLoading)
            {
                return;
            }

            StatusMessage = null;

            // The preview follows the form as long as what is typed makes a valid label.
            var errors = new Dictionary<string, string>();
            var settings = ReadForm(errors);
            if (errors.Count == 0)
            {
                ShowPreview(settings);
            }
        }

        private void Save()
        {
            var settings = ReadAndShowErrors();
            if (settings == null)
            {
                return;
            }

            try
            {
                _settingsService.Save(settings);
            }
            catch (BusinessRuleException ex)
            {
                StatusMessage = ex.Message;
                return;
            }

            StatusMessage = "Paramètres enregistrés.";
        }

        private void PrintTest()
        {
            var settings = ReadAndShowErrors();
            if (settings == null)
            {
                return;
            }

            StatusMessage = _labelPrintService.PrintTestLabel(settings)
                ? "Étiquette de test envoyée à l'imprimante."
                : null;
        }

        private LabelSettings ReadAndShowErrors()
        {
            var errors = new Dictionary<string, string>();
            var settings = ReadForm(errors);
            ShowErrors(errors);

            if (errors.Count > 0)
            {
                StatusMessage = null;
                return null;
            }

            ShowPreview(settings);
            return settings;
        }

        // A text that is not a number keeps its own message; the ranges are checked by the service.
        private LabelSettings ReadForm(IDictionary<string, string> errors)
        {
            var settings = new LabelSettings
            {
                WidthMm = ReadDecimal(Width, LabelSettingsService.WidthField, errors),
                HeightMm = ReadDecimal(Height, LabelSettingsService.HeightField, errors),
                MarginTopMm = ReadDecimal(MarginTop, LabelSettingsService.MarginTopField, errors),
                MarginLeftMm = ReadDecimal(MarginLeft, LabelSettingsService.MarginLeftField, errors),
                HorizontalGapMm = ReadDecimal(HorizontalGap, LabelSettingsService.HorizontalGapField, errors),
                VerticalGapMm = ReadDecimal(VerticalGap, LabelSettingsService.VerticalGapField, errors),
                Columns = ReadInteger(Columns, LabelSettingsService.ColumnsField, errors),
                Rows = ReadInteger(Rows, LabelSettingsService.RowsField, errors),
                PrinterName = _selectedPrinter?.Name
            };

            foreach (var error in _settingsService.Validate(settings))
            {
                if (!errors.ContainsKey(error.Field))
                {
                    errors[error.Field] = error.Message;
                }
            }

            return settings;
        }

        private static decimal ReadDecimal(FormField field, string name, IDictionary<string, string> errors)
        {
            var text = (field.Text ?? string.Empty).Trim().Replace(',', '.');
            if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                return decimal.Round(value, 2);
            }

            errors[name] = InvalidNumberMessage;
            return 0m;
        }

        private static int ReadInteger(FormField field, string name, IDictionary<string, string> errors)
        {
            if (int.TryParse((field.Text ?? string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            errors[name] = InvalidIntegerMessage;
            return 0;
        }

        private void ShowErrors(IDictionary<string, string> errors)
        {
            Width.Error = MessageFor(errors, LabelSettingsService.WidthField);
            Height.Error = MessageFor(errors, LabelSettingsService.HeightField);
            MarginTop.Error = MessageFor(errors, LabelSettingsService.MarginTopField);
            MarginLeft.Error = MessageFor(errors, LabelSettingsService.MarginLeftField);
            HorizontalGap.Error = MessageFor(errors, LabelSettingsService.HorizontalGapField);
            VerticalGap.Error = MessageFor(errors, LabelSettingsService.VerticalGapField);
            Columns.Error = MessageFor(errors, LabelSettingsService.ColumnsField);
            Rows.Error = MessageFor(errors, LabelSettingsService.RowsField);
        }

        private void ShowPreview(LabelSettings settings)
        {
            PreviewLabels = Enumerable.Repeat(LabelContent.CreateSample(), settings.LabelsPerPage).ToList();
            PreviewSettings = settings;
        }

        private static string MessageFor(IDictionary<string, string> errors, string field)
        {
            return errors.TryGetValue(field, out var message) ? message : null;
        }

        private static string FormatMillimetres(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
        }
    }
}
