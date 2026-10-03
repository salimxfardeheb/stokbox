using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Stokbox.App.Printing;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Xunit;
using ZXing;
using ZXing.Common;

namespace Stokbox.App.Tests
{
    public class LabelRenderingTests
    {
        private const string Barcode = "2000000001234";

        [Theory]
        [InlineData(50, 30, 300)]
        [InlineData(50, 30, 203)]
        [InlineData(32, 20, 203)]
        [InlineData(63.5, 33.9, 600)]
        [InlineData(100, 50, 203)]
        public void The_rendered_barcode_decodes_to_the_original_value(double widthMm, double heightMm, int dpi)
        {
            Sta.Run(() =>
            {
                var label = new LabelElement(new LabelContent("Café moulu 250 g", "1 250,00 DA", Barcode), widthMm, heightMm, dpi);

                var decoded = Decode(Render(label, dpi));

                Assert.NotNull(decoded);
                Assert.Equal(BarcodeFormat.EAN_13, decoded.BarcodeFormat);
                Assert.Equal(Barcode, decoded.Text);
            });
        }

        [Fact]
        public void Every_generated_barcode_decodes_from_a_label_printed_at_203_dpi()
        {
            Sta.Run(() =>
            {
                foreach (var sequence in new long[] { 1, 42, 987654, 1234567890, BarcodeGenerator.MaxSequenceValue })
                {
                    var barcode = BarcodeGenerator.FromSequenceValue(sequence);
                    var label = new LabelElement(new LabelContent("Produit", "35,50 DA", barcode), 50, 30, 203);

                    var decoded = Decode(Render(label, 203));

                    Assert.NotNull(decoded);
                    Assert.Equal(barcode, decoded.Text);
                }
            });
        }

        [Fact]
        public void A_long_name_and_a_large_price_leave_the_barcode_readable()
        {
            Sta.Run(() =>
            {
                var content = new LabelContent(
                    "Désignation très longue qui ne tient pas sur deux lignes de l'étiquette et doit donc être tronquée",
                    "999 999 999,99 DA",
                    Barcode);

                var decoded = Decode(Render(new LabelElement(content, 50, 30, 203), 203));

                Assert.NotNull(decoded);
                Assert.Equal(Barcode, decoded.Text);
            });
        }

        [Theory]
        [InlineData(50, 30)]
        [InlineData(63.5, 33.9)]
        [InlineData(100, 150)]
        public void A_label_has_exactly_the_configured_size(double widthMm, double heightMm)
        {
            Sta.Run(() =>
            {
                var label = new LabelElement(LabelContent.CreateSample(), widthMm, heightMm, 203);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Assert.Equal(widthMm, label.DesiredSize.Width * 25.4 / 96, 3);
                Assert.Equal(heightMm, label.DesiredSize.Height * 25.4 / 96, 3);
            });
        }

        [Theory]
        [InlineData(203)]
        [InlineData(300)]
        [InlineData(600)]
        public void A_barcode_module_is_a_whole_number_of_printer_dots(int dpi)
        {
            // 47 mm: the room left for the barcode on a 50 mm label.
            var available = 47 * 96 / 25.4;

            var module = LabelElement.ComputeModuleWidth(available, dpi);
            var dots = module * dpi / 96;

            Assert.Equal(Math.Round(dots), dots, 6);
            Assert.True(dots >= 2, "Module de " + dots + " point(s)");

            // Bars and quiet zones (11 + 95 + 7 modules) fit in the room available.
            Assert.True(module * 113 <= available + 1e-6);

            // Never thinner than 75 % of the nominal 0.33 mm module.
            Assert.True(module * 25.4 / 96 >= 0.33 * 0.75);
        }

        [Fact]
        public void The_preview_page_has_the_configured_dimensions_and_places_the_labels_on_the_grid()
        {
            Sta.Run(() =>
            {
                var settings = new LabelSettings
                {
                    WidthMm = 63.5m,
                    HeightMm = 33.9m,
                    MarginTopMm = 12.9m,
                    MarginLeftMm = 7.2m,
                    HorizontalGapMm = 2.5m,
                    Columns = 3,
                    Rows = 8
                };
                var labels = Enumerable.Repeat(LabelContent.CreateSample(), 5).ToList();

                var page = LabelDocumentBuilder.CreatePage(labels, settings, 203, false);

                Assert.Equal(209.9, ToMm(page.Width), 3);
                Assert.Equal(297.0, ToMm(page.Height), 3);

                var elements = page.Children.OfType<LabelElement>().ToList();
                Assert.Equal(5, elements.Count);
                Assert.All(elements, element =>
                {
                    Assert.Equal(63.5, ToMm(element.Width), 3);
                    Assert.Equal(33.9, ToMm(element.Height), 3);
                });

                // Fourth label: first column of the second row.
                Assert.Equal(7.2, ToMm(Canvas.GetLeft(elements[3])), 3);
                Assert.Equal(12.9 + 33.9, ToMm(Canvas.GetTop(elements[3])), 3);

                // Fifth label: second column of the second row.
                Assert.Equal(7.2 + 63.5 + 2.5, ToMm(Canvas.GetLeft(elements[4])), 3);
            });
        }

        [Fact]
        public void The_document_has_one_page_per_group_of_labels_at_the_configured_page_size()
        {
            Sta.Run(() =>
            {
                var settings = new LabelSettings { Columns = 2, Rows = 2 };
                var product = new Product { Name = "Café moulu", Barcode = Barcode, SalePriceCents = 125000 };
                var labels = LabelDocumentBuilder.Expand(new[] { new LabelPrintItem(product, 9) });

                var document = LabelDocumentBuilder.CreateDocument(labels, settings, 203);

                Assert.Equal(9, labels.Count);
                Assert.Equal("1 250,00 DA", labels[0].PriceText);
                Assert.Equal(3, document.Pages.Count);
                Assert.Equal(100.0, ToMm(document.DocumentPaginator.PageSize.Width), 3);
                Assert.Equal(60.0, ToMm(document.DocumentPaginator.PageSize.Height), 3);
            });
        }

        [Fact]
        public void The_thermal_default_prints_one_label_per_page_of_the_size_of_the_label()
        {
            Sta.Run(() =>
            {
                var labels = Enumerable.Repeat(LabelContent.CreateSample(), 3).ToList();

                var document = LabelDocumentBuilder.CreateDocument(labels, new LabelSettings(), 203);

                Assert.Equal(3, document.Pages.Count);
                Assert.Equal(50.0, ToMm(document.DocumentPaginator.PageSize.Width), 3);
                Assert.Equal(30.0, ToMm(document.DocumentPaginator.PageSize.Height), 3);
            });
        }

        [Fact]
        public void The_test_label_carries_a_valid_barcode()
        {
            Assert.True(BarcodeGenerator.IsValidEan13(LabelContent.CreateSample().Barcode));
        }

        private static double ToMm(double dip)
        {
            return dip * 25.4 / 96;
        }

        // Rasterises the label as a printer of the given resolution would.
        private static BitmapSource Render(LabelElement label, int dpi)
        {
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            label.Arrange(new Rect(label.DesiredSize));
            label.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(label.Width * dpi / 96),
                (int)Math.Ceiling(label.Height * dpi / 96),
                dpi,
                dpi,
                PixelFormats.Pbgra32);
            bitmap.Render(label);
            return bitmap;
        }

        private static Result Decode(BitmapSource bitmap)
        {
            var stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);

            var reader = new BarcodeReaderGeneric
            {
                Options = new DecodingOptions
                {
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.EAN_13 },
                    TryHarder = true
                }
            };

            return reader.Decode(new RGBLuminanceSource(
                pixels,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                RGBLuminanceSource.BitmapFormat.BGRA32));
        }
    }
}
