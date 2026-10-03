using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Stokbox.App.Printing;
using Stokbox.Core.Entities;
using Xunit;

namespace Stokbox.App.Tests
{
    public class ReceiptRenderingTests
    {
        private static readonly double Width72Mm = 72 * 96 / 25.4;

        [Fact]
        public void The_receipt_shows_the_shop_the_sale_its_lines_and_the_amounts()
        {
            Sta.Run(() =>
            {
                var texts = TextsOf(ReceiptDocumentBuilder.CreateReceipt(SampleSale(), Shop(), Width72Mm));

                Assert.Equal(
                    new[]
                    {
                        "Boutique El Baraka",
                        "12 rue Didouche Mourad, Alger",
                        "Tél. : 021 00 00 00",
                        "Vente V-20261003-0007",
                        SampleSale().CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                        "Café moulu 250 g",
                        "  2 × 520,50 DA",
                        "1 041,00 DA",
                        "Eau minérale 1,5 L",
                        "  1 × 35,00 DA",
                        "35,00 DA",
                        "TOTAL",
                        "1 076,00 DA",
                        "Reçu",
                        "1 100,00 DA",
                        "Rendu",
                        "24,00 DA",
                        "Merci de votre visite"
                    },
                    texts);
            });
        }

        [Fact]
        public void A_shop_without_details_prints_a_receipt_without_header()
        {
            Sta.Run(() =>
            {
                var texts = TextsOf(ReceiptDocumentBuilder.CreateReceipt(SampleSale(), new ReceiptSettings(), Width72Mm));

                Assert.Equal("Vente V-20261003-0007", texts[0]);
                Assert.Equal("Merci de votre visite", texts.Last());
            });
        }

        [Fact]
        public void The_receipt_is_72_mm_wide_and_as_tall_as_its_content()
        {
            Sta.Run(() =>
            {
                var shortDocument = ReceiptDocumentBuilder.CreateDocument(SampleSale(), Shop(), 0, 0, Width72Mm);

                var longSale = SampleSale();
                longSale.Lines = Enumerable.Repeat(longSale.Lines[0], 30).ToList();
                var longDocument = ReceiptDocumentBuilder.CreateDocument(longSale, Shop(), 0, 0, Width72Mm);

                Assert.Equal(1, shortDocument.Pages.Count);
                Assert.Equal(72.0, shortDocument.DocumentPaginator.PageSize.Width * 25.4 / 96, 3);
                Assert.Equal(72.0, longDocument.DocumentPaginator.PageSize.Width * 25.4 / 96, 3);
                Assert.True(longDocument.DocumentPaginator.PageSize.Height > shortDocument.DocumentPaginator.PageSize.Height * 2);
            });
        }

        [Fact]
        public void The_receipt_starts_at_the_printable_area_given_by_the_driver()
        {
            Sta.Run(() =>
            {
                var document = ReceiptDocumentBuilder.CreateDocument(SampleSale(), Shop(), 15, 10, Width72Mm);

                Assert.Equal(15 + Width72Mm, document.DocumentPaginator.PageSize.Width, 3);
            });
        }

        [Fact]
        public void The_test_receipt_is_consistent()
        {
            var sale = ReceiptDocumentBuilder.CreateSampleSale();

            Assert.Equal(sale.TotalCents, sale.Lines.Sum(line => line.LineTotalCents));
            Assert.All(sale.Lines, line => Assert.Equal(line.Quantity * line.UnitPriceCents, line.LineTotalCents));
            Assert.Equal(sale.ReceivedCents - sale.TotalCents, sale.ChangeCents);
        }

        private static ReceiptSettings Shop()
        {
            return new ReceiptSettings
            {
                ShopName = "Boutique El Baraka",
                ShopAddress = "12 rue Didouche Mourad, Alger",
                ShopPhone = "021 00 00 00",
                PrinterName = "Ticket"
            };
        }

        private static Sale SampleSale()
        {
            return new Sale
            {
                Number = "V-20261003-0007",
                CreatedAtUtc = new DateTime(2026, 10, 3, 12, 30, 0, DateTimeKind.Utc),
                TotalCents = 107600,
                ReceivedCents = 110000,
                ChangeCents = 2400,
                Lines = new List<SaleLine>
                {
                    new SaleLine { ProductName = "Café moulu 250 g", Quantity = 2, UnitPriceCents = 52050, LineTotalCents = 104100 },
                    new SaleLine { ProductName = "Eau minérale 1,5 L", Quantity = 1, UnitPriceCents = 3500, LineTotalCents = 3500 }
                }
            };
        }

        // Every text of the receipt, from top to bottom and left to right.
        private static List<string> TextsOf(DependencyObject root)
        {
            var texts = new List<string>();
            Collect(root, texts);
            return texts;
        }

        private static void Collect(DependencyObject element, List<string> texts)
        {
            if (element is TextBlock textBlock)
            {
                texts.Add(textBlock.Text);
                return;
            }

            if (element is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                {
                    Collect(child, texts);
                }
            }
        }
    }
}
