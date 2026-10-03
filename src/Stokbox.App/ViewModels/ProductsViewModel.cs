using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.App.Services;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    public sealed class ProductsViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly IDialogService _dialogs;

        private string _searchText = string.Empty;
        private CategoryFilterOption _selectedCategoryFilter;
        private bool _showArchived;
        private Product _selectedProduct;

        // Set while the lists are rebuilt: the bindings then push transient values that must not trigger a search.
        private bool _isReloading;

        public ProductsViewModel(ProductService productService, CategoryService categoryService, IDialogService dialogs)
        {
            _productService = productService;
            _categoryService = categoryService;
            _dialogs = dialogs;

            Products = new ObservableCollection<Product>();
            CategoryFilters = new ObservableCollection<CategoryFilterOption>();

            NewCommand = new RelayCommand(CreateProduct);
            EditCommand = new RelayCommand(EditProduct, () => SelectedProduct != null);
            ToggleArchiveCommand = new RelayCommand(ToggleArchive, () => SelectedProduct != null);
            ManageCategoriesCommand = new RelayCommand(ManageCategories);
        }

        public ObservableCollection<Product> Products { get; }

        public ObservableCollection<CategoryFilterOption> CategoryFilters { get; }

        public RelayCommand NewCommand { get; }

        public RelayCommand EditCommand { get; }

        public RelayCommand ToggleArchiveCommand { get; }

        public RelayCommand ManageCategoriesCommand { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ReloadProducts();
                }
            }
        }

        public CategoryFilterOption SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                if (_isReloading)
                {
                    return;
                }

                if (SetProperty(ref _selectedCategoryFilter, value))
                {
                    ReloadProducts();
                }
            }
        }

        public bool ShowArchived
        {
            get => _showArchived;
            set
            {
                if (SetProperty(ref _showArchived, value))
                {
                    ReloadProducts();
                }
            }
        }

        public Product SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (_isReloading)
                {
                    return;
                }

                if (SetProperty(ref _selectedProduct, value))
                {
                    OnSelectionChanged();
                }
            }
        }

        public string ArchiveButtonText => SelectedProduct != null && SelectedProduct.IsArchived ? "Désarchiver" : "Archiver";

        /// <summary>
        /// Reads the categories and the products again; called each time the screen is shown.
        /// </summary>
        public void Refresh()
        {
            ReloadCategoryFilters();
            ReloadProducts();
        }

        private void ReloadCategoryFilters()
        {
            var selectedId = _selectedCategoryFilter?.Id;

            _isReloading = true;
            try
            {
                CategoryFilters.Clear();
                CategoryFilters.Add(new CategoryFilterOption(null, "Toutes les catégories"));
                foreach (var category in _categoryService.GetAll())
                {
                    CategoryFilters.Add(new CategoryFilterOption(category.Id, category.Name));
                }
            }
            finally
            {
                _isReloading = false;
            }

            // A deleted category falls back to "every category".
            _selectedCategoryFilter = CategoryFilters.FirstOrDefault(f => f.Id == selectedId) ?? CategoryFilters[0];
            OnPropertyChanged(nameof(SelectedCategoryFilter));
        }

        private void ReloadProducts()
        {
            ReloadProducts(_selectedProduct?.Id);
        }

        private void ReloadProducts(long? productIdToSelect)
        {
            var found = _productService.Search(new ProductSearchCriteria
            {
                Text = SearchText,
                CategoryId = _selectedCategoryFilter?.Id,
                IncludeArchived = ShowArchived
            });

            _isReloading = true;
            try
            {
                Products.Clear();
                foreach (var product in found)
                {
                    Products.Add(product);
                }
            }
            finally
            {
                _isReloading = false;
            }

            _selectedProduct = Products.FirstOrDefault(p => p.Id == productIdToSelect);
            OnPropertyChanged(nameof(SelectedProduct));
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            OnPropertyChanged(nameof(ArchiveButtonText));
            EditCommand.NotifyCanExecuteChanged();
            ToggleArchiveCommand.NotifyCanExecuteChanged();
        }

        private void CreateProduct()
        {
            if (_categoryService.GetAll().Count == 0)
            {
                _dialogs.ShowWarning("Créez d'abord une catégorie : chaque produit doit appartenir à une catégorie.");
                ManageCategories();
                if (_categoryService.GetAll().Count == 0)
                {
                    return;
                }
            }

            var form = new ProductFormViewModel(_productService, _categoryService.GetAll(), null);
            if (_dialogs.ShowProductForm(form))
            {
                ReloadProducts(form.SavedProductId);
            }
        }

        private void EditProduct()
        {
            if (SelectedProduct == null)
            {
                return;
            }

            var product = _productService.GetById(SelectedProduct.Id);
            if (product == null)
            {
                _dialogs.ShowWarning("Ce produit n'existe plus.");
                ReloadProducts();
                return;
            }

            var form = new ProductFormViewModel(_productService, _categoryService.GetAll(), product);
            if (_dialogs.ShowProductForm(form))
            {
                ReloadProducts(form.SavedProductId);
            }
        }

        private void ToggleArchive()
        {
            var product = SelectedProduct;
            if (product == null)
            {
                return;
            }

            try
            {
                if (product.IsArchived)
                {
                    _productService.Unarchive(product.Id);
                }
                else
                {
                    if (!_dialogs.Confirm("Archiver « " + product.Name + " » ?\n\nUn produit archivé ne peut plus être vendu. Il pourra être désarchivé plus tard."))
                    {
                        return;
                    }

                    _productService.Archive(product.Id);
                }
            }
            catch (BusinessRuleException ex)
            {
                _dialogs.ShowWarning(ex.Message);
            }

            ReloadProducts();
        }

        private void ManageCategories()
        {
            _dialogs.ShowCategories(new CategoriesViewModel(_categoryService, _dialogs));

            // Categories may have been added, renamed or deleted.
            Refresh();
        }
    }
}
