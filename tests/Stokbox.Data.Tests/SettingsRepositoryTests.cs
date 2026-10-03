using System;
using System.Collections.Generic;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Data.Migrations;
using Stokbox.Data.Repositories;
using Xunit;

namespace Stokbox.Data.Tests
{
    public class SettingsRepositoryTests : IDisposable
    {
        private readonly TempDatabase _database = new TempDatabase();
        private readonly SettingsRepository _settings;

        public SettingsRepositoryTests()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();
            _settings = new SettingsRepository(_database.ConnectionFactory);
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        [Fact]
        public void A_new_database_has_no_setting()
        {
            Assert.Empty(_settings.GetAll());
        }

        [Fact]
        public void Saved_values_are_read_back_and_replace_the_previous_ones()
        {
            _settings.Save(new Dictionary<string, string> { { "a", "1" }, { "b", "2" } });
            _settings.Save(new Dictionary<string, string> { { "b", "3" }, { "c", "" } });

            var values = _settings.GetAll();

            Assert.Equal(3, values.Count);
            Assert.Equal("1", values["a"]);
            Assert.Equal("3", values["b"]);
            Assert.Equal(string.Empty, values["c"]);
        }

        [Fact]
        public void Label_settings_survive_a_round_trip_through_the_database()
        {
            var service = new LabelSettingsService(_settings);
            service.Save(new LabelSettings
            {
                WidthMm = 63.5m,
                HeightMm = 33.9m,
                MarginTopMm = 12.9m,
                MarginLeftMm = 7.2m,
                HorizontalGapMm = 2.5m,
                Columns = 3,
                Rows = 8,
                PrinterName = "Étiquettes (thermique)"
            });

            var settings = new LabelSettingsService(new SettingsRepository(_database.ConnectionFactory)).Get();

            Assert.Equal(63.5m, settings.WidthMm);
            Assert.Equal(33.9m, settings.HeightMm);
            Assert.Equal(12.9m, settings.MarginTopMm);
            Assert.Equal(7.2m, settings.MarginLeftMm);
            Assert.Equal(2.5m, settings.HorizontalGapMm);
            Assert.Equal(0m, settings.VerticalGapMm);
            Assert.Equal(3, settings.Columns);
            Assert.Equal(8, settings.Rows);
            Assert.Equal("Étiquettes (thermique)", settings.PrinterName);
        }
    }
}
