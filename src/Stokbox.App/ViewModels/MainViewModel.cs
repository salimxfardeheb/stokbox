using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokbox.App.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private const string DashboardKey = "dashboard";
        private const string SaleKey = "sale";
        private const string ProductsKey = "products";
        private const string StockEntriesKey = "stock-entries";
        private const string LabelsKey = "labels";
        private const string HistoryKey = "history";
        private const string SettingsKey = "settings";

        private readonly DashboardViewModel _dashboard;
        private readonly SaleViewModel _sale;
        private readonly ProductsViewModel _products;
        private readonly StockEntriesViewModel _stockEntries;
        private readonly LabelsViewModel _labels;
        private readonly HistoryViewModel _history;
        private readonly SettingsViewModel _settings;
        private NavigationItem _selectedMenuItem;
        private object _currentPage;

        public MainViewModel(
            DashboardViewModel dashboard,
            SaleViewModel sale,
            ProductsViewModel products,
            StockEntriesViewModel stockEntries,
            LabelsViewModel labels,
            HistoryViewModel history,
            SettingsViewModel settings)
        {
            _dashboard = dashboard;
            _sale = sale;
            _products = products;
            _stockEntries = stockEntries;
            _labels = labels;
            _history = history;
            _settings = settings;
            MenuItems = new[]
            {
                new NavigationItem(DashboardKey, "Tableau de bord"),
                new NavigationItem(SaleKey, "Vente"),
                new NavigationItem(ProductsKey, "Produits"),
                new NavigationItem(StockEntriesKey, "Entrées de stock"),
                new NavigationItem(LabelsKey, "Étiquettes"),
                new NavigationItem(HistoryKey, "Historique"),
                new NavigationItem(SettingsKey, "Paramètres")
            };

            _dashboard.OutOfStockRequested += (sender, e) => ShowOutOfStockProducts();

            // The sale screen is the home screen, even though the dashboard comes first in the menu.
            _selectedMenuItem = MenuItem(SaleKey);
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

        private NavigationItem MenuItem(string key)
        {
            return MenuItems.First(item => item.Key == key);
        }

        // The filter is set first: showing the products screen then reads the list once, already filtered.
        private void ShowOutOfStockProducts()
        {
            _products.FilterOutOfStock();
            SelectedMenuItem = MenuItem(ProductsKey);
        }

        private void ShowPageOf(NavigationItem item)
        {
            if (item != null && item.Key == DashboardKey)
            {
                _dashboard.Refresh();
                CurrentPage = _dashboard;
            }
            else if (item != null && item.Key == SaleKey)
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
            else if (item != null && item.Key == HistoryKey)
            {
                _history.Refresh();
                CurrentPage = _history;
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
