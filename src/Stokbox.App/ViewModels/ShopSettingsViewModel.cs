using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.Core.Services;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Settings > Shop: the name, address and phone printed at the top of the receipts.
    /// </summary>
    public sealed class ShopSettingsViewModel : ObservableObject
    {
        private readonly ReceiptSettingsService _settingsService;

        private string _shopName = string.Empty;
        private string _shopAddress = string.Empty;
        private string _shopPhone = string.Empty;
        private string _shopNameError;
        private string _statusMessage;

        public ShopSettingsViewModel(ReceiptSettingsService settingsService)
        {
            _settingsService = settingsService;
            SaveCommand = new RelayCommand(Save);
        }

        public RelayCommand SaveCommand { get; }

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

        public string ShopNameError
        {
            get => _shopNameError;
            private set => SetProperty(ref _shopNameError, value);
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
            ShopName = settings.ShopName ?? string.Empty;
            ShopAddress = settings.ShopAddress ?? string.Empty;
            ShopPhone = settings.ShopPhone ?? string.Empty;
            ShopNameError = null;
            StatusMessage = null;
        }

        private void SetInput(ref string field, string value, string propertyName)
        {
            if (SetProperty(ref field, value, propertyName))
            {
                StatusMessage = null;
            }
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(ShopName))
            {
                ShopNameError = "Le nom de la boutique est obligatoire.";
                return;
            }

            // The receipt printer belongs to another tab: it is kept as saved.
            var settings = _settingsService.Get();
            settings.ShopName = ShopName;
            settings.ShopAddress = ShopAddress;
            settings.ShopPhone = ShopPhone;
            _settingsService.Save(settings);

            ShopNameError = null;
            StatusMessage = "Paramètres enregistrés.";
        }
    }
}
