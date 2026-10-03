using System.Linq;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Tests.Fakes;
using Stokbox.Core.Validation;
using Xunit;

namespace Stokbox.Core.Tests
{
    public class LabelSettingsTests
    {
        private readonly InMemorySettingsRepository _repository = new InMemorySettingsRepository();
        private readonly LabelSettingsService _service;

        public LabelSettingsTests()
        {
            _service = new LabelSettingsService(_repository);
        }

        [Fact]
        public void Defaults_are_one_50_by_30_label_per_page_and_no_printer()
        {
            var settings = _service.Get();

            Assert.Equal(50m, settings.WidthMm);
            Assert.Equal(30m, settings.HeightMm);
            Assert.Equal(0m, settings.MarginTopMm);
            Assert.Equal(0m, settings.MarginLeftMm);
            Assert.Equal(0m, settings.HorizontalGapMm);
            Assert.Equal(0m, settings.VerticalGapMm);
            Assert.Equal(1, settings.Columns);
            Assert.Equal(1, settings.Rows);
            Assert.Null(settings.PrinterName);
            Assert.Empty(_service.Validate(settings));
        }

        [Fact]
        public void Saved_settings_are_read_back()
        {
            _service.Save(A4Sheet());

            var settings = _service.Get();

            Assert.Equal(63.5m, settings.WidthMm);
            Assert.Equal(33.9m, settings.HeightMm);
            Assert.Equal(12.9m, settings.MarginTopMm);
            Assert.Equal(7.2m, settings.MarginLeftMm);
            Assert.Equal(2.5m, settings.HorizontalGapMm);
            Assert.Equal(0m, settings.VerticalGapMm);
            Assert.Equal(3, settings.Columns);
            Assert.Equal(8, settings.Rows);
            Assert.Equal("Imprimante du bureau", settings.PrinterName);
        }

        [Fact]
        public void Clearing_the_printer_goes_back_to_asking_each_time()
        {
            _service.Save(A4Sheet());
            var settings = _service.Get();
            settings.PrinterName = "  ";

            _service.Save(settings);

            Assert.Null(_service.Get().PrinterName);
        }

        [Fact]
        public void Unusable_stored_values_fall_back_to_the_defaults()
        {
            _repository.Values["label.width_mm"] = "abc";
            _repository.Values["label.height_mm"] = "5";
            _repository.Values["label.columns"] = "0";
            _repository.Values["label.rows"] = "8";

            var settings = _service.Get();

            Assert.Equal(50m, settings.WidthMm);
            Assert.Equal(30m, settings.HeightMm);
            Assert.Equal(1, settings.Columns);
            Assert.Equal(8, settings.Rows);
        }

        [Theory]
        [InlineData(LabelSettingsService.WidthField, "31.9")]
        [InlineData(LabelSettingsService.WidthField, "300.1")]
        [InlineData(LabelSettingsService.HeightField, "19.9")]
        [InlineData(LabelSettingsService.HeightField, "301")]
        [InlineData(LabelSettingsService.MarginTopField, "-0.1")]
        [InlineData(LabelSettingsService.MarginTopField, "100.1")]
        [InlineData(LabelSettingsService.MarginLeftField, "-1")]
        [InlineData(LabelSettingsService.MarginLeftField, "101")]
        [InlineData(LabelSettingsService.HorizontalGapField, "-1")]
        [InlineData(LabelSettingsService.HorizontalGapField, "101")]
        [InlineData(LabelSettingsService.VerticalGapField, "-1")]
        [InlineData(LabelSettingsService.VerticalGapField, "101")]
        [InlineData(LabelSettingsService.ColumnsField, "0")]
        [InlineData(LabelSettingsService.ColumnsField, "11")]
        [InlineData(LabelSettingsService.RowsField, "0")]
        [InlineData(LabelSettingsService.RowsField, "31")]
        public void A_value_out_of_range_is_refused_and_not_saved(string field, string value)
        {
            var settings = With(field, value);

            var error = Assert.Single(_service.Validate(settings));
            Assert.Equal(field, error.Field);
            Assert.Throws<ValidationException>(() => _service.Save(settings));
            Assert.Empty(_repository.Values);
        }

        [Theory]
        [InlineData(LabelSettingsService.WidthField, "32")]
        [InlineData(LabelSettingsService.WidthField, "300")]
        [InlineData(LabelSettingsService.HeightField, "20")]
        [InlineData(LabelSettingsService.HeightField, "300")]
        [InlineData(LabelSettingsService.MarginTopField, "0")]
        [InlineData(LabelSettingsService.MarginTopField, "100")]
        [InlineData(LabelSettingsService.MarginLeftField, "0")]
        [InlineData(LabelSettingsService.MarginLeftField, "100")]
        [InlineData(LabelSettingsService.HorizontalGapField, "0")]
        [InlineData(LabelSettingsService.HorizontalGapField, "100")]
        [InlineData(LabelSettingsService.VerticalGapField, "0")]
        [InlineData(LabelSettingsService.VerticalGapField, "100")]
        [InlineData(LabelSettingsService.ColumnsField, "1")]
        [InlineData(LabelSettingsService.ColumnsField, "10")]
        [InlineData(LabelSettingsService.RowsField, "1")]
        [InlineData(LabelSettingsService.RowsField, "30")]
        public void A_value_at_the_edge_of_its_range_is_accepted(string field, string value)
        {
            Assert.Empty(_service.Validate(With(field, value)));
        }

        [Fact]
        public void Every_faulty_field_is_reported_at_once()
        {
            var settings = new LabelSettings { WidthMm = 0, HeightMm = 0, Columns = 0, Rows = 0 };

            Assert.Equal(
                new[]
                {
                    LabelSettingsService.WidthField,
                    LabelSettingsService.HeightField,
                    LabelSettingsService.ColumnsField,
                    LabelSettingsService.RowsField
                },
                _service.Validate(settings).Select(e => e.Field));
        }

        [Fact]
        public void A_single_label_fills_its_page()
        {
            var settings = new LabelSettings();

            Assert.Equal(1, settings.LabelsPerPage);
            Assert.Equal(50m, settings.PageWidthMm);
            Assert.Equal(30m, settings.PageHeightMm);
            Assert.Equal(0m, settings.GetLabelLeftMm(0));
            Assert.Equal(0m, settings.GetLabelTopMm(0));
        }

        [Fact]
        public void A_sheet_places_its_labels_after_the_margins_and_the_gaps()
        {
            var settings = A4Sheet();

            Assert.Equal(24, settings.LabelsPerPage);
            Assert.Equal(209.9m, settings.PageWidthMm);
            Assert.Equal(297m, settings.PageHeightMm);
            Assert.Equal(7.2m, settings.GetLabelLeftMm(0));
            Assert.Equal(73.2m, settings.GetLabelLeftMm(1));
            Assert.Equal(139.2m, settings.GetLabelLeftMm(2));
            Assert.Equal(12.9m, settings.GetLabelTopMm(0));
            Assert.Equal(250.2m, settings.GetLabelTopMm(7));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(24, 1)]
        [InlineData(25, 2)]
        [InlineData(48, 2)]
        [InlineData(49, 3)]
        public void Labels_fill_as_many_pages_as_needed(int labelCount, int expectedPages)
        {
            Assert.Equal(expectedPages, A4Sheet().CountPages(labelCount));
        }

        private static LabelSettings A4Sheet()
        {
            return new LabelSettings
            {
                WidthMm = 63.5m,
                HeightMm = 33.9m,
                MarginTopMm = 12.9m,
                MarginLeftMm = 7.2m,
                HorizontalGapMm = 2.5m,
                VerticalGapMm = 0m,
                Columns = 3,
                Rows = 8,
                PrinterName = "Imprimante du bureau"
            };
        }

        private static LabelSettings With(string field, string value)
        {
            var number = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            var settings = new LabelSettings();
            switch (field)
            {
                case LabelSettingsService.WidthField: settings.WidthMm = number; break;
                case LabelSettingsService.HeightField: settings.HeightMm = number; break;
                case LabelSettingsService.MarginTopField: settings.MarginTopMm = number; break;
                case LabelSettingsService.MarginLeftField: settings.MarginLeftMm = number; break;
                case LabelSettingsService.HorizontalGapField: settings.HorizontalGapMm = number; break;
                case LabelSettingsService.VerticalGapField: settings.VerticalGapMm = number; break;
                case LabelSettingsService.ColumnsField: settings.Columns = (int)number; break;
                case LabelSettingsService.RowsField: settings.Rows = (int)number; break;
            }

            return settings;
        }
    }
}
