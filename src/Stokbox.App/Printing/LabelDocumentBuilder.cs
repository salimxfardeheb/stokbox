using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Stokbox.Core.Entities;

namespace Stokbox.App.Printing
{
    /// <summary>
    /// Lays the labels out on pages according to the label settings.
    /// </summary>
    public static class LabelDocumentBuilder
    {
        /// <summary>
        /// One label per copy, in the order of the items.
        /// </summary>
        public static IReadOnlyList<LabelContent> Expand(IEnumerable<LabelPrintItem> items)
        {
            return items
                .SelectMany(item => Enumerable.Repeat(LabelContent.FromProduct(item.Product), item.Copies))
                .ToList();
        }

        /// <summary>
        /// One page at its real size, filled row by row with the given labels (at most one page of them).
        /// </summary>
        /// <param name="showCellOutlines">Draws the edge of every label position; for the screen only.</param>
        public static Canvas CreatePage(IReadOnlyList<LabelContent> labels, LabelSettings settings, double deviceDpi, bool showCellOutlines)
        {
            var page = new Canvas
            {
                Width = Units.MmToDip(settings.PageWidthMm),
                Height = Units.MmToDip(settings.PageHeightMm),
                Background = Brushes.White
            };

            for (var index = 0; index < settings.LabelsPerPage; index++)
            {
                var left = Units.MmToDip(settings.GetLabelLeftMm(index % settings.Columns));
                var top = Units.MmToDip(settings.GetLabelTopMm(index / settings.Columns));

                if (index < labels.Count)
                {
                    Place(page, new LabelElement(labels[index], (double)settings.WidthMm, (double)settings.HeightMm, deviceDpi), left, top);
                }

                if (showCellOutlines)
                {
                    Place(page, CreateOutline(settings), left, top);
                }
            }

            return page;
        }

        /// <summary>
        /// The whole print job: as many pages as the labels need.
        /// </summary>
        public static FixedDocument CreateDocument(IReadOnlyList<LabelContent> labels, LabelSettings settings, double deviceDpi)
        {
            var pageSize = new Size(Units.MmToDip(settings.PageWidthMm), Units.MmToDip(settings.PageHeightMm));
            var document = new FixedDocument();
            document.DocumentPaginator.PageSize = pageSize;

            for (var first = 0; first < labels.Count; first += settings.LabelsPerPage)
            {
                var labelsOfPage = labels.Skip(first).Take(settings.LabelsPerPage).ToList();

                var fixedPage = new FixedPage { Width = pageSize.Width, Height = pageSize.Height, Background = Brushes.White };
                fixedPage.Children.Add(CreatePage(labelsOfPage, settings, deviceDpi, false));
                fixedPage.Measure(pageSize);
                fixedPage.Arrange(new Rect(pageSize));
                fixedPage.UpdateLayout();

                var pageContent = new PageContent();
                ((IAddChild)pageContent).AddChild(fixedPage);
                document.Pages.Add(pageContent);
            }

            return document;
        }

        private static void Place(Canvas page, UIElement element, double left, double top)
        {
            Canvas.SetLeft(element, left);
            Canvas.SetTop(element, top);
            page.Children.Add(element);
        }

        private static Rectangle CreateOutline(LabelSettings settings)
        {
            return new Rectangle
            {
                Width = Units.MmToDip(settings.WidthMm),
                Height = Units.MmToDip(settings.HeightMm),
                Stroke = new SolidColorBrush(Color.FromRgb(0xB8, 0xC2, 0xCC)),
                StrokeThickness = 0.6,
                StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0 }),
                SnapsToDevicePixels = true
            };
        }
    }
}
