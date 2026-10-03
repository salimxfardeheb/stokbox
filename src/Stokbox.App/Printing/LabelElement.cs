using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Stokbox.Core.Services;
using ZXing.OneD;

namespace Stokbox.App.Printing
{
    /// <summary>
    /// One price label drawn as vectors: name on two lines at most, price in large type,
    /// EAN-13 barcode and its digits. The same element serves the preview and the printing.
    /// </summary>
    public sealed class LabelElement : FrameworkElement
    {
        /// <summary>
        /// Resolution assumed when the printer's one is unknown: the coarsest common thermal head.
        /// </summary>
        public const double DefaultDeviceDpi = 203.0;

        // EAN-13: 95 modules of bars, plus the blank quiet zones the scanner needs on each side.
        private const int BarModules = 95;
        private const int LeftQuietZoneModules = 11;
        private const int RightQuietZoneModules = 7;
        private const int TotalModules = LeftQuietZoneModules + BarModules + RightQuietZoneModules;

        private const double PaddingMm = 1.5;

        // Nominal EAN-13 module is 0.33 mm; wider bars only waste room on the label.
        private const double MaxModuleMm = 0.45;

        private static readonly Typeface RegularTypeface = new Typeface(
            new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Typeface BoldTypeface = new Typeface(
            new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        private static readonly CultureInfo TextCulture = CultureInfo.GetCultureInfo("fr-FR");

        private readonly LabelContent _content;
        private readonly double _deviceDpi;

        public LabelElement(LabelContent content, double widthMm, double heightMm, double deviceDpi)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _deviceDpi = deviceDpi > 0 ? deviceDpi : DefaultDeviceDpi;

            Width = Units.MmToDip(widthMm);
            Height = Units.MmToDip(heightMm);
            ClipToBounds = true;
        }

        /// <summary>
        /// Width of one barcode module, in WPF units, for a barcode (quiet zones included) fitting the given width.
        /// It is a whole number of printer dots: a bar that falls between two dots is printed too thin or too thick,
        /// which is what makes a barcode unreadable on a 203 dpi printer.
        /// </summary>
        public static double ComputeModuleWidth(double availableWidth, double deviceDpi)
        {
            var dot = 96.0 / deviceDpi;
            var widest = Math.Min(Units.MmToDip(MaxModuleMm), availableWidth / TotalModules);
            var dots = Math.Floor(widest / dot + 1e-9);

            // Label too narrow for even one dot per module: keep the proportions, the validation forbids such sizes.
            return dots >= 1 ? dots * dot : widest;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(Width, Height);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var width = Width;
            var height = Height;
            var padding = Units.MmToDip(PaddingMm);
            var innerWidth = width - 2 * padding;
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            drawingContext.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

            var name = CreateText(_content.Name, RegularTypeface, Clamp(height * 0.085, 2.0, 3.2), pixelsPerDip);
            name.MaxTextWidth = innerWidth;
            name.MaxLineCount = 2;
            name.Trimming = TextTrimming.CharacterEllipsis;
            name.TextAlignment = TextAlignment.Center;

            // A long price shrinks until it fits the width rather than being cut.
            var priceSize = Clamp(height * 0.2, 3.5, 9.0);
            var smallestPriceSize = Units.MmToDip(2.5);
            var price = CreateText(_content.PriceText, BoldTypeface, priceSize, pixelsPerDip);
            while (price.Width > innerWidth && priceSize > smallestPriceSize)
            {
                priceSize *= 0.92;
                price.SetFontSize(priceSize);
            }

            var digits = CreateText(_content.Barcode, RegularTypeface, Clamp(height * 0.075, 1.9, 2.8), pixelsPerDip);

            var y = padding;
            drawingContext.DrawText(name, new Point(padding, y));
            y += name.Height;

            drawingContext.DrawText(price, new Point((width - price.Width) / 2, y));
            y += price.Height + Units.MmToDip(0.4);

            var digitsTop = height - padding - digits.Height;
            DrawBars(drawingContext, padding, innerWidth, y, digitsTop - y);
            drawingContext.DrawText(digits, new Point((width - digits.Width) / 2, digitsTop));
        }

        private void DrawBars(DrawingContext drawingContext, double left, double availableWidth, double top, double barsHeight)
        {
            if (barsHeight <= 0 || !BarcodeGenerator.IsValidEan13(_content.Barcode))
            {
                return;
            }

            var modules = new EAN13Writer().encode(_content.Barcode);
            var moduleWidth = ComputeModuleWidth(availableWidth, _deviceDpi);
            var barcodeWidth = moduleWidth * (LeftQuietZoneModules + modules.Length + RightQuietZoneModules);
            var firstBarLeft = left + (availableWidth - barcodeWidth) / 2 + LeftQuietZoneModules * moduleWidth;

            // One rectangle per run of dark modules: adjacent modules never leave a hairline between them.
            var index = 0;
            while (index < modules.Length)
            {
                if (!modules[index])
                {
                    index++;
                    continue;
                }

                var start = index;
                while (index < modules.Length && modules[index])
                {
                    index++;
                }

                drawingContext.DrawRectangle(
                    Brushes.Black,
                    null,
                    new Rect(firstBarLeft + start * moduleWidth, top, (index - start) * moduleWidth, barsHeight));
            }
        }

        private static FormattedText CreateText(string text, Typeface typeface, double fontSize, double pixelsPerDip)
        {
            return new FormattedText(
                text ?? string.Empty,
                TextCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                pixelsPerDip);
        }

        // Font size proportional to the label height, kept between two sizes given in millimetres.
        private static double Clamp(double value, double minMm, double maxMm)
        {
            return Math.Max(Units.MmToDip(minMm), Math.Min(Units.MmToDip(maxMm), value));
        }
    }
}
