using System;
using System.Collections.Generic;
using System.Linq;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Validation;

namespace Stokbox.Core.Services
{
    public sealed class CategoryService
    {
        public const string NameField = "Name";

        private const string NotFoundMessage = "Cette catégorie n'existe plus.";

        private readonly ICategoryRepository _categories;

        public CategoryService(ICategoryRepository categories)
        {
            _categories = categories ?? throw new ArgumentNullException(nameof(categories));
        }

        public IReadOnlyList<Category> GetAll()
        {
            return _categories.GetAll();
        }

        public Category Create(string name)
        {
            var cleanName = ValidateName(name, null);
            var id = _categories.Insert(cleanName);
            return new Category { Id = id, Name = cleanName };
        }

        public void Rename(long id, string name)
        {
            var cleanName = ValidateName(name, id);
            if (!_categories.Rename(id, cleanName))
            {
                throw new BusinessRuleException(NotFoundMessage);
            }
        }

        /// <summary>
        /// A category that still has products, archived ones included, cannot be deleted.
        /// </summary>
        public void Delete(long id)
        {
            if (_categories.CountProducts(id) > 0)
            {
                throw new CategoryNotEmptyException();
            }

            if (!_categories.Delete(id))
            {
                throw new BusinessRuleException(NotFoundMessage);
            }
        }

        private string ValidateName(string name, long? ownId)
        {
            var cleanName = (name ?? string.Empty).Trim();
            if (cleanName.Length == 0)
            {
                throw new ValidationException(NameField, "Le nom de la catégorie est obligatoire.");
            }

            var folded = TextNormalizer.Fold(cleanName);
            if (_categories.GetAll().Any(c => c.Id != ownId && TextNormalizer.Fold(c.Name) == folded))
            {
                throw new ValidationException(NameField, "Une catégorie porte déjà ce nom.");
            }

            return cleanName;
        }
    }
}
