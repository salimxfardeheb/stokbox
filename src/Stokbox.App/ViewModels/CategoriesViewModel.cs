using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.App.Services;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    public sealed class CategoriesViewModel : ObservableObject
    {
        private readonly CategoryService _categoryService;
        private readonly IDialogService _dialogs;

        private Category _selectedCategory;
        private string _nameText = string.Empty;
        private string _errorMessage;

        // Set while the list is rebuilt: the binding then pushes a transient null selection.
        private bool _isReloading;

        public CategoriesViewModel(CategoryService categoryService, IDialogService dialogs)
        {
            _categoryService = categoryService;
            _dialogs = dialogs;

            Categories = new ObservableCollection<Category>();
            AddCommand = new RelayCommand(Add);
            RenameCommand = new RelayCommand(Rename, () => SelectedCategory != null);
            DeleteCommand = new RelayCommand(Delete, () => SelectedCategory != null);

            Reload(null);
        }

        public ObservableCollection<Category> Categories { get; }

        public RelayCommand AddCommand { get; }

        public RelayCommand RenameCommand { get; }

        public RelayCommand DeleteCommand { get; }

        public Category SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_isReloading)
                {
                    return;
                }

                if (SetProperty(ref _selectedCategory, value))
                {
                    OnSelectionChanged();
                }
            }
        }

        /// <summary>
        /// Name typed by the user: the new category to add, or the new name of the selected one.
        /// </summary>
        public string NameText
        {
            get => _nameText;
            set => SetProperty(ref _nameText, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        private void Add()
        {
            Run(() =>
            {
                _categoryService.Create(NameText);
                Reload(null);
            });
        }

        private void Rename()
        {
            var category = SelectedCategory;
            if (category == null)
            {
                return;
            }

            Run(() =>
            {
                _categoryService.Rename(category.Id, NameText);
                Reload(category.Id);
            });
        }

        private void Delete()
        {
            var category = SelectedCategory;
            if (category == null || !_dialogs.Confirm("Supprimer la catégorie « " + category.Name + " » ?"))
            {
                return;
            }

            Run(() =>
            {
                _categoryService.Delete(category.Id);
                Reload(null);
            });
        }

        // A refused operation shows its reason under the field and leaves everything as it was.
        private void Run(Action operation)
        {
            try
            {
                operation();
                ErrorMessage = null;
            }
            catch (BusinessRuleException ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private void Reload(long? categoryIdToSelect)
        {
            var categories = _categoryService.GetAll();

            _isReloading = true;
            try
            {
                Categories.Clear();
                foreach (var category in categories)
                {
                    Categories.Add(category);
                }
            }
            finally
            {
                _isReloading = false;
            }

            _selectedCategory = Categories.FirstOrDefault(c => c.Id == categoryIdToSelect);
            OnPropertyChanged(nameof(SelectedCategory));
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            NameText = SelectedCategory == null ? string.Empty : SelectedCategory.Name;
            ErrorMessage = null;
            RenameCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }
}
