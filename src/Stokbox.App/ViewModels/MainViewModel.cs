using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokbox.App.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private NavigationItem _selectedMenuItem;

        public MainViewModel()
        {
            MenuItems = new[]
            {
                new NavigationItem("sale", "Vente"),
                new NavigationItem("products", "Produits"),
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
            set => SetProperty(ref _selectedMenuItem, value);
        }
    }
}
