using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.App.Services;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Settings > Shop and receipt: what heads the receipts, and the receipt printer.
    /// </summary>
    public sealed class ReceiptSettingsViewModel : ObservableObject
    {
        private readonly ReceiptSettingsService _settingsService;
        private readonly IReceiptPrintService _receiptPrintService;
        private readonly IPrinterCatalog _printers;

        private string _shopName = string.Empty;
        private string _shopAddress = string.Empty;
        private string _shopPhone = string.Empty;
        private PrinterOption _selectedPrinter;
        private string _statusMessage;
        private bool _isLoading;

        public ReceiptSettingsViewModel(
            ReceiptSettingsService settingsService,
            IReceiptPrintService receiptPrintService,
            IPrinterCatalog printers)
        {
            _settingsService = settingsService;
            _receiptPrintService = receiptPrintService;
            _printers = printers;

            PrinterOptions = new ObservableCollection<PrinterOption>();
            SaveCommand = new RelayCommand(Save);
            PrintTestCommand = new RelayCommand(PrintTest);
        }

        public ObservableCollection<PrinterOption> PrinterOptions { get; }

        public RelayCommand SaveCommand { get; }

        public RelayCommand PrintTestCommand { get; }

        public string ShopName
        {
            get => _shopName;
            set => SetInput(ref _shopName, value, nameof(ShopName));
        }

        public string ShopAddress
        {
            get => _shopAddress;
            set => SetInput(ref _shopAddress, value, nameof(ShopAddress));
        }

        public string ShopPhone
        {
            get => _shopPhone;
            set => SetInput(ref _shopPhone, value, nameof(ShopPhone));
        }

        public PrinterOption SelectedPrinter
        {
            get => _selectedPrinter;
            set
            {
                if (_isLoading)
                {
                    return;
                }

                if (SetProperty(ref _selectedPrinter, value))
                {
                    StatusMessage = null;
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Fills the form with the saved settings; called each time the screen is shown.
        /// </summary>
        public void Load()
        {
            var settings = _settingsService.Get();

            _isLoading = true;
            try
            {
                PrinterOptions.Clear();
                PrinterOptions.Add(new PrinterOption(null, "(Aucune : ticket désactivé)"));
                var installed = _printers.GetPrinterNames();
                foreach (var name in installed)
                {
                    PrinterOptions.Add(new PrinterOption(name, name));
                }

                // A saved printer that Windows no longer lists stays visible instead of silently vanishing.
                if (settings.PrinterName != null && !installed.Contains(settings.PrinterName, StringComparer.OrdinalIgnoreCase))
                {
                    PrinterOptions.Add(new PrinterOption(settings.PrinterName, settings.PrinterName + " (introuvable)"));
                }
            }
            finally
            {
                _isLoading = false;
            }

            ShopName = settings.ShopName ?? string.Empty;
            ShopAddress = settings.ShopAddress ?? string.Empty;
            ShopPhone = settings.ShopPhone ?? string.Empty;

            _selectedPrinter = PrinterOptions.FirstOrDefault(
                    option => string.Equals(option.Name, settings.PrinterName, StringComparison.OrdinalIgnoreCase))
                ?? PrinterOptions[0];
            OnPropertyChanged(nameof(SelectedPrinter));

            StatusMessage = null;
        }

        private void SetInput(ref string field, string value, string propertyName)
        {
            if (SetProperty(ref field, value, propertyName))
            {
                StatusMessage = null;
            }
        }

        private ReceiptSettings ReadForm()
        {
            return new ReceiptSettings
            {
                ShopName = ShopName,
                ShopAddress = ShopAddress,
                ShopPhone = ShopPhone,
                PrinterName = _selectedPrinter?.Name
            };
        }

        private void Save()
        {
            _settingsService.Save(ReadForm());
            StatusMessage = "Paramètres enregistrés.";
        }

        private void PrintTest()
        {
            var settings = ReadForm();
            if (settings.PrinterName == null)
            {
                StatusMessage = "Choisissez d'abord l'imprimante ticket.";
                return;
            }

            StatusMessage = _receiptPrintService.PrintTest(settings)
                ? "Ticket de test envoyé à l'imprimante."
                : null;
        }
    }
}
