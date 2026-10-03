using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Stokbox.Core;
using Stokbox.Core.Entities;

namespace Stokbox.App.Printing
{
    /// <summary>
    /// Lays out the receipt of a sale for an 80 mm receipt printer.
    /// </summary>
    public static class ReceiptDocumentBuilder
    {
        /// <summary>
        /// Width a receipt printer actually prints on 80 mm paper.
        /// </summary>
        public const double PrintableWidthMm = 72.0;

        // Blank paper after the last line, so that the text clears the cutter.
        private const double BottomFeedMm = 8.0;

        private const double NormalSize = 11.5;
        private const double TotalSize = 15.0;
        private const double ShopNameSize = 16.0;

        private static readonly FontFamily Font = new FontFamily("Segoe UI");

        /// <summary>
        /// The receipt as one column of the given width; its height follows its content.
        /// </summary>
        public static FrameworkElement CreateReceipt(Sale sale, ReceiptSettings shop, double width)
        {
            var receipt = new StackPanel { Width = width, Background = Brushes.White };

            if (!string.IsNullOrWhiteSpace(shop.ShopName))
            {
                receipt.Children.Add(Text(shop.ShopName, ShopNameSize, FontWeights.Bold, TextAlignment.Center));
            }

            if (!string.IsNullOrWhiteSpace(shop.ShopAddress))
            {
                receipt.Children.Add(Text(shop.ShopAddress, NormalSize, FontWeights.Normal, TextAlignment.Center));
            }

            if (!string.IsNullOrWhiteSpace(shop.ShopPhone))
            {
                receipt.Children.Add(Text("Tél. : " + shop.ShopPhone, NormalSize, FontWeights.Normal, TextAlignment.Center));
            }

            receipt.Children.Add(Separator());
            receipt.Children.Add(Row(
                "Vente " + sale.Number,
                sale.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                NormalSize,
                FontWeights.Normal));

            // A reprinted receipt of a cancelled sale must not pass for a proof of purchase.
            if (sale.Status == Sale.StatusCancelled)
            {
                receipt.Children.Add(Text("*** VENTE ANNULÉE ***", NormalSize, FontWeights.Bold, TextAlignment.Center));
            }

            receipt.Children.Add(Separator());

            foreach (var line in sale.Lines)
            {
                receipt.Children.Add(Text(line.ProductName, NormalSize, FontWeights.Normal, TextAlignment.Left));
                receipt.Children.Add(Row(
                    "  " + line.Quantity.ToString(CultureInfo.InvariantCulture) + " × " + Money.Format(line.UnitPriceCents),
                    Money.Format(line.LineTotalCents),
                    NormalSize,
                    FontWeights.Normal));
            }

            receipt.Children.Add(Separator());
            receipt.Children.Add(Row("TOTAL", Money.Format(sale.TotalCents), TotalSize, FontWeights.Bold));
            receipt.Children.Add(Row("Reçu", Money.Format(sale.ReceivedCents), NormalSize, FontWeights.Normal));
            receipt.Children.Add(Row("Rendu", Money.Format(sale.ChangeCents), NormalSize, FontWeights.Normal));
            receipt.Children.Add(Separator());

            var thanks = Text("Merci de votre visite", NormalSize, FontWeights.Normal, TextAlignment.Center);
            thanks.Margin = new Thickness(0, 4, 0, 0);
            receipt.Children.Add(thanks);

            return receipt;
        }

        /// <summary>
        /// One page as tall as the receipt, placed at the top-left corner of the printable area.
        /// </summary>
        public static FixedDocument CreateDocument(Sale sale, ReceiptSettings shop, double left, double top, double width)
        {
            var receipt = CreateReceipt(sale, shop, width);
            receipt.Measure(new Size(width, double.PositiveInfinity));

            var pageSize = new Size(
                left + width,
                top + receipt.DesiredSize.Height + Units.MmToDip(BottomFeedMm));

            var page = new FixedPage { Width = pageSize.Width, Height = pageSize.Height, Background = Brushes.White };
            FixedPage.SetLeft(receipt, left);
            FixedPage.SetTop(receipt, top);
            page.Children.Add(receipt);
            page.Measure(pageSize);
            page.Arrange(new Rect(pageSize));
            page.UpdateLayout();

            var document = new FixedDocument();
            document.DocumentPaginator.PageSize = pageSize;
            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(page);
            document.Pages.Add(pageContent);

            return document;
        }

        /// <summary>
        /// Made-up sale printed by the test button of the settings.
        /// </summary>
        public static Sale CreateSampleSale()
        {
            return new Sale
            {
                Number = "V-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-0000",
                CreatedAtUtc = DateTime.UtcNow,
                TotalCents = 135500,
                ReceivedCents = 150000,
                ChangeCents = 14500,
                Status = Sale.StatusValidated,
                Lines = new[]
                {
                    new SaleLine { ProductName = "Article de test", Quantity = 1, UnitPriceCents = 125000, LineTotalCents = 125000 },
                    new SaleLine { ProductName = "Second article de test, à la désignation plus longue", Quantity = 3, UnitPriceCents = 3500, LineTotalCents = 10500 }
                }
            };
        }

        private static TextBlock Text(string text, double size, FontWeight weight, TextAlignment alignment)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = Font,
                FontSize = size,
                FontWeight = weight,
                Foreground = Brushes.Black,
                TextAlignment = alignment,
                TextWrapping = TextWrapping.Wrap
            };
        }

        // A label on the left, an amount on the right; the label wraps, the amount never does.
        private static Grid Row(string left, string right, double size, FontWeight weight)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = Text(left, size, weight, TextAlignment.Left);
            var amount = Text(right, size, weight, TextAlignment.Right);
            amount.TextWrapping = TextWrapping.NoWrap;
            amount.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(amount, 1);

            row.Children.Add(label);
            row.Children.Add(amount);
            return row;
        }

        private static Rectangle Separator()
        {
            return new Rectangle { Height = 1, Fill = Brushes.Black, Margin = new Thickness(0, 4, 0, 4) };
        }
    }
}
