using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokbox.App.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private const string ProductsKey = "products";
        private const string StockEntriesKey = "stock-entries";

        private readonly ProductsViewModel _products;
        private readonly StockEntriesViewModel _stockEntries;
        private NavigationItem _selectedMenuItem;
        private object _currentPage;

        public MainViewModel(ProductsViewModel products, StockEntriesViewModel stockEntries)
        {
            _products = products;
            _stockEntries = stockEntries;
            MenuItems = new[]
            {
                new NavigationItem("sale", "Vente"),
                new NavigationItem(ProductsKey, "Produits"),
                new NavigationItem(StockEntriesKey, "Entrées de stock"),
                new NavigationItem("labels", "Étiquettes"),
                new NavigationItem("history", "Historique"),
                new NavigationItem("settings", "Paramètres")
            };
            _selectedMenuItem = MenuItems[0];
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
            if (item != null && item.Key == ProductsKey)
            {
                _products.Refresh();
                CurrentPage = _products;
            }
            else if (item != null && item.Key == StockEntriesKey)
            {
                CurrentPage = _stockEntries;
            }
            else
            {
                CurrentPage = null;
            }
        }
    }
}
