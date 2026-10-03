using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokbox.App.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private const string ProductsKey = "products";
        private const string StockEntriesKey = "stock-entries";
        private const string LabelsKey = "labels";
        private const string SettingsKey = "settings";

        private readonly ProductsViewModel _products;
        private readonly StockEntriesViewModel _stockEntries;
        private readonly LabelsViewModel _labels;
        private readonly LabelSettingsViewModel _labelSettings;
        private NavigationItem _selectedMenuItem;
        private object _currentPage;

        public MainViewModel(
            ProductsViewModel products,
            StockEntriesViewModel stockEntries,
            LabelsViewModel labels,
            LabelSettingsViewModel labelSettings)
        {
            _products = products;
            _stockEntries = stockEntries;
            _labels = labels;
            _labelSettings = labelSettings;
            MenuItems = new[]
            {
                new NavigationItem("sale", "Vente"),
                new NavigationItem(ProductsKey, "Produits"),
                new NavigationItem(StockEntriesKey, "Entrées de stock"),
                new NavigationItem(LabelsKey, "Étiquettes"),
                new NavigationItem("history", "Historique"),
                new NavigationItem(SettingsKey, "Paramètres")
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
            else if (item != null && item.Key == LabelsKey)
            {
                _labels.Refresh();
                CurrentPage = _labels;
            }
            else if (item != null && item.Key == SettingsKey)
            {
                _labelSettings.Load();
                CurrentPage = _labelSettings;
            }
            else
            {
                CurrentPage = null;
            }
        }
    }
}
