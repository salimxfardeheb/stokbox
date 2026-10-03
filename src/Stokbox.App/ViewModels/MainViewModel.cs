using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokbox.App.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private const string SaleKey = "sale";
        private const string ProductsKey = "products";
        private const string StockEntriesKey = "stock-entries";
        private const string LabelsKey = "labels";
        private const string SettingsKey = "settings";

        private readonly SaleViewModel _sale;
        private readonly ProductsViewModel _products;
        private readonly StockEntriesViewModel _stockEntries;
        private readonly LabelsViewModel _labels;
        private readonly SettingsViewModel _settings;
        private NavigationItem _selectedMenuItem;
        private object _currentPage;

        public MainViewModel(
            SaleViewModel sale,
            ProductsViewModel products,
            StockEntriesViewModel stockEntries,
            LabelsViewModel labels,
            SettingsViewModel settings)
        {
            _sale = sale;
            _products = products;
            _stockEntries = stockEntries;
            _labels = labels;
            _settings = settings;
            MenuItems = new[]
            {
                new NavigationItem(SaleKey, "Vente"),
                new NavigationItem(ProductsKey, "Produits"),
                new NavigationItem(StockEntriesKey, "Entrées de stock"),
                new NavigationItem(LabelsKey, "Étiquettes"),
                new NavigationItem("history", "Historique"),
                new NavigationItem(SettingsKey, "Paramètres")
            };

            // The sale screen is the home screen.
            _selectedMenuItem = MenuItems[0];
            ShowPageOf(_selectedMenuItem);
        }

        public IReadOnlyList<NavigationItem> MenuItems { get; }

        public NavigationItem SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (SetProperty(ref _selectedMenuItem, value))
                {
                    ShowPageOf(value);
                }
            }
        }

        /// <summary>
        /// ViewModel of the screen shown in the content area; null for the screens that do not exist yet.
        /// </summary>
        public object CurrentPage
        {
            get => _currentPage;
            private set => SetProperty(ref _currentPage, value);
        }

        private void ShowPageOf(NavigationItem item)
        {
            if (item != null && item.Key == SaleKey)
            {
                _sale.Activate();
                CurrentPage = _sale;
            }
            else if (item != null && item.Key == ProductsKey)
            {
                _products.Refresh();
                CurrentPage = _products;
            }
            else if (item != null && item.Key == StockEntriesKey)
            {
                CurrentPage = _stockEntries;
            }
            else if (item != null && item.Key == LabelsKey)
            {
                _labels.Refresh();
                CurrentPage = _labels;
            }
            else if (item != null && item.Key == SettingsKey)
            {
                _settings.Load();
                CurrentPage = _settings;
            }
            else
            {
                CurrentPage = null;
            }
        }
    }
}
