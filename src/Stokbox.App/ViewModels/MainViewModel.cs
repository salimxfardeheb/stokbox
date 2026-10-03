using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokbox.App.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private const string ProductsKey = "products";

        private readonly ProductsViewModel _products;
        private NavigationItem _selectedMenuItem;
        private object _currentPage;

        public MainViewModel(ProductsViewModel products)
        {
            _products = products;
            MenuItems = new[]
            {
                new NavigationItem("sale", "Vente"),
                new NavigationItem(ProductsKey, "Produits"),
                new NavigationItem("stock-entries", "Entrées de stock"),
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
            else
            {
                CurrentPage = null;
            }
        }
    }
}
