using System;
using System.Collections.Generic;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;

namespace Stokbox.Core.Services
{
    public sealed class ReceiptSettingsService
    {
        private const string ShopNameKey = "shop.name";
        private const string ShopAddressKey = "shop.address";
        private const string ShopPhoneKey = "shop.phone";
        private const string PrinterNameKey = "receipt.printer_name";

        private readonly ISettingsRepository _settings;

        public ReceiptSettingsService(ISettingsRepository settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// The saved settings; a text never entered is null.
        /// </summary>
        public ReceiptSettings Get()
        {
            var stored = _settings.GetAll();

            return new ReceiptSettings
            {
                ShopName = Read(stored, ShopNameKey),
                ShopAddress = Read(stored, ShopAddressKey),
                ShopPhone = Read(stored, ShopPhoneKey),
                PrinterName = Read(stored, PrinterNameKey)
            };
        }

        public void Save(ReceiptSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _settings.Save(new Dictionary<string, string>
            {
                { ShopNameKey, Clean(settings.ShopName) },
                { ShopAddressKey, Clean(settings.ShopAddress) },
                { ShopPhoneKey, Clean(settings.ShopPhone) },
                { PrinterNameKey, Clean(settings.PrinterName) }
            });
        }

        private static string Read(IReadOnlyDictionary<string, string> stored, string key)
        {
            return stored.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
