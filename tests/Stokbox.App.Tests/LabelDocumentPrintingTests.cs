using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using Stokbox.App.Printing;
using Stokbox.Core.Entities;
using Xunit;

namespace Stokbox.App.Tests
{
    public class LabelDocumentPrintingTests
    {
        /// <summary>
        /// Printing hands the document to the driver through the XPS serializer: writing it to an XPS file
        /// goes through the same path, without needing a printer.
        /// </summary>
        [Fact]
        public void The_document_is_written_to_the_print_path_as_vectors_at_the_page_size()
        {
            var file = Path.Combine(Path.GetTempPath(), "stokbox-tests-" + Guid.NewGuid().ToString("N") + ".xps");
            try
            {
                Sta.Run(() =>
                {
                    var labels = Enumerable.Repeat(new LabelContent("Café moulu 250 g", "1 250,00 DA", "2000000001234"), 3).ToList();
                    var document = LabelDocumentBuilder.CreateDocument(labels, new LabelSettings(), 203);

                    using (var xps = new XpsDocument(file, FileAccess.ReadWrite))
                    {
                        XpsDocumentWriter writer = XpsDocument.CreateXpsDocumentWriter(xps);
                        writer.Write(document.DocumentPaginator);
                    }
                });

                using (var package = Package.Open(file, FileMode.Open, FileAccess.Read))
                {
                    var pages = package.GetParts()
                        .Where(part => part.Uri.OriginalString.EndsWith(".fpage", StringComparison.OrdinalIgnoreCase))
                        .Select(ReadText)
                        .ToList();

                    Assert.Equal(3, pages.Count);
                    Assert.All(pages, page =>
                    {
                        // 50 × 30 mm in 1/96 inch.
                        Assert.Contains("Width=\"188.97", page);
                        Assert.Contains("Height=\"113.38", page);

                        // Bars are filled paths and texts are glyphs: nothing is turned into a picture.
                        Assert.Contains("<Path", page);
                        Assert.Contains("<Glyphs", page);
                        Assert.DoesNotContain("ImageBrush", page);
                    });
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        private static string ReadText(PackagePart part)
        {
            using (var reader = new StreamReader(part.GetStream(FileMode.Open, FileAccess.Read), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
