using System;
using System.Collections.Generic;
using System.Globalization;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Validation;

namespace Stokbox.Core.Services
{
    public sealed class LabelSettingsService
    {
        public const string WidthField = "Width";
        public const string HeightField = "Height";
        public const string MarginTopField = "MarginTop";
        public const string MarginLeftField = "MarginLeft";
        public const string HorizontalGapField = "HorizontalGap";
        public const string VerticalGapField = "VerticalGap";
        public const string ColumnsField = "Columns";
        public const string RowsField = "Rows";

        // Below 32 mm the EAN-13 bars become too thin for a 203 dpi thermal printer.
        public const decimal MinWidthMm = 32m;
        public const decimal MinHeightMm = 20m;
        public const decimal MaxSizeMm = 300m;
        public const decimal MaxMarginMm = 100m;
        public const int MaxColumns = 10;
        public const int MaxRows = 30;

        private const string WidthKey = "label.width_mm";
        private const string HeightKey = "label.height_mm";
        private const string MarginTopKey = "label.margin_top_mm";
        private const string MarginLeftKey = "label.margin_left_mm";
        private const string HorizontalGapKey = "label.gap_horizontal_mm";
        private const string VerticalGapKey = "label.gap_vertical_mm";
        private const string ColumnsKey = "label.columns";
        private const string RowsKey = "label.rows";
        private const string PrinterNameKey = "label.printer_name";

        private readonly ISettingsRepository _settings;

        public LabelSettingsService(ISettingsRepository settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// The saved settings; a value that is missing or unusable is replaced by its default.
        /// </summary>
        public LabelSettings Get()
        {
            var stored = _settings.GetAll();
            var defaults = new LabelSettings();

            stored.TryGetValue(PrinterNameKey, out var printerName);

            return new LabelSettings
            {
                WidthMm = ReadDecimal(stored, WidthKey, MinWidthMm, MaxSizeMm, defaults.WidthMm),
                HeightMm = ReadDecimal(stored, HeightKey, MinHeightMm, MaxSizeMm, defaults.HeightMm),
                MarginTopMm = ReadDecimal(stored, MarginTopKey, 0m, MaxMarginMm, defaults.MarginTopMm),
                MarginLeftMm = ReadDecimal(stored, MarginLeftKey, 0m, MaxMarginMm, defaults.MarginLeftMm),
                HorizontalGapMm = ReadDecimal(stored, HorizontalGapKey, 0m, MaxMarginMm, defaults.HorizontalGapMm),
                VerticalGapMm = ReadDecimal(stored, VerticalGapKey, 0m, MaxMarginMm, defaults.VerticalGapMm),
                Columns = ReadInt(stored, ColumnsKey, 1, MaxColumns, defaults.Columns),
                Rows = ReadInt(stored, RowsKey, 1, MaxRows, defaults.Rows),
                PrinterName = string.IsNullOrWhiteSpace(printerName) ? null : printerName
            };
        }

        /// <summary>
        /// At most one error per field; empty when the settings are valid.
        /// </summary>
        public IReadOnlyList<ValidationError> Validate(LabelSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var errors = new List<ValidationError>();

            CheckRange(errors, WidthField, settings.WidthMm, MinWidthMm, MaxSizeMm, "La largeur doit être comprise entre 32 et 300 mm.");
            CheckRange(errors, HeightField, settings.HeightMm, MinHeightMm, MaxSizeMm, "La hauteur doit être comprise entre 20 et 300 mm.");
            CheckRange(errors, MarginTopField, settings.MarginTopMm, 0m, MaxMarginMm, "La marge haute doit être comprise entre 0 et 100 mm.");
            CheckRange(errors, MarginLeftField, settings.MarginLeftMm, 0m, MaxMarginMm, "La marge gauche doit être comprise entre 0 et 100 mm.");
            CheckRange(errors, HorizontalGapField, settings.HorizontalGapMm, 0m, MaxMarginMm, "L'espacement horizontal doit être compris entre 0 et 100 mm.");
            CheckRange(errors, VerticalGapField, settings.VerticalGapMm, 0m, MaxMarginMm, "L'espacement vertical doit être compris entre 0 et 100 mm.");
            CheckRange(errors, ColumnsField, settings.Columns, 1, MaxColumns, "Le nombre de colonnes doit être compris entre 1 et 10.");
            CheckRange(errors, RowsField, settings.Rows, 1, MaxRows, "Le nombre de lignes doit être compris entre 1 et 30.");

            return errors;
        }

        public void Save(LabelSettings settings)
        {
            var errors = Validate(settings);
            if (errors.Count > 0)
            {
                throw new ValidationException(errors);
            }

            _settings.Save(new Dictionary<string, string>
            {
                { WidthKey, Format(settings.WidthMm) },
                { HeightKey, Format(settings.HeightMm) },
                { MarginTopKey, Format(settings.MarginTopMm) },
                { MarginLeftKey, Format(settings.MarginLeftMm) },
                { HorizontalGapKey, Format(settings.HorizontalGapMm) },
                { VerticalGapKey, Format(settings.VerticalGapMm) },
                { ColumnsKey, settings.Columns.ToString(CultureInfo.InvariantCulture) },
                { RowsKey, settings.Rows.ToString(CultureInfo.InvariantCulture) },
                { PrinterNameKey, string.IsNullOrWhiteSpace(settings.PrinterName) ? string.Empty : settings.PrinterName.Trim() }
            });
        }

        private static void CheckRange(List<ValidationError> errors, string field, decimal value, decimal min, decimal max, string message)
        {
            if (value < min || value > max)
            {
                errors.Add(new ValidationError(field, message));
            }
        }

        private static string Format(decimal millimetres)
        {
            return millimetres.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static decimal ReadDecimal(IReadOnlyDictionary<string, string> stored, string key, decimal min, decimal max, decimal fallback)
        {
            return stored.TryGetValue(key, out var text)
                && decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
                && value >= min
                && value <= max
                    ? value
                    : fallback;
        }

        private static int ReadInt(IReadOnlyDictionary<string, string> stored, string key, int min, int max, int fallback)
        {
            return stored.TryGetValue(key, out var text)
                && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                && value >= min
                && value <= max
                    ? value
                    : fallback;
        }
    }
}
