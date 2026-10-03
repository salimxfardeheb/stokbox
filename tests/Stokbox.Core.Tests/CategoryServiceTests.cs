using System.Linq;
using Stokbox.Core.Services;
using Stokbox.Core.Tests.Fakes;
using Stokbox.Core.Validation;
using Xunit;

namespace Stokbox.Core.Tests
{
    public class CategoryServiceTests
    {
        private readonly InMemoryCategoryRepository _repository = new InMemoryCategoryRepository();
        private readonly CategoryService _service;

        public CategoryServiceTests()
        {
            _service = new CategoryService(_repository);
        }

        [Fact]
        public void A_category_is_created_with_its_trimmed_name()
        {
            var category = _service.Create("  Boissons ");

            Assert.Equal("Boissons", category.Name);
            Assert.Equal("Boissons", _repository.GetById(category.Id).Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void A_category_without_a_name_is_refused(string name)
        {
            var error = Assert.Throws<ValidationException>(() => _service.Create(name));

            Assert.Equal(CategoryService.NameField, error.Errors.Single().Field);
            Assert.Empty(_repository.GetAll());
        }

        [Theory]
        [InlineData("Épicerie")]
        [InlineData("epicerie")]
        [InlineData(" ÉPICERIE ")]
        public void Two_categories_cannot_have_the_same_name(string duplicate)
        {
            _service.Create("Épicerie");

            Assert.Throws<ValidationException>(() => _service.Create(duplicate));
            Assert.Single(_repository.GetAll());
        }

        [Fact]
        public void A_category_is_renamed()
        {
            var category = _service.Create("Boissons");

            _service.Rename(category.Id, "Boissons fraîches");

            Assert.Equal("Boissons fraîches", _repository.GetById(category.Id).Name);
        }

        [Fact]
        public void A_category_can_be_renamed_to_its_own_name_with_another_case()
        {
            var category = _service.Create("boissons");

            _service.Rename(category.Id, "Boissons");

            Assert.Equal("Boissons", _repository.GetById(category.Id).Name);
        }

        [Fact]
        public void Renaming_to_an_empty_name_or_to_the_name_of_another_category_is_refused()
        {
            var drinks = _service.Create("Boissons");
            _service.Create("Épicerie");

            Assert.Throws<ValidationException>(() => _service.Rename(drinks.Id, " "));
            Assert.Throws<ValidationException>(() => _service.Rename(drinks.Id, "epicerie"));
            Assert.Equal("Boissons", _repository.GetById(drinks.Id).Name);
        }

        [Fact]
        public void Renaming_a_missing_category_is_refused()
        {
            Assert.Throws<BusinessRuleException>(() => _service.Rename(99, "Boissons"));
        }

        [Fact]
        public void An_empty_category_is_deleted()
        {
            var category = _service.Create("Boissons");

            _service.Delete(category.Id);

            Assert.Empty(_repository.GetAll());
        }

        [Fact]
        public void A_category_containing_products_cannot_be_deleted()
        {
            var category = _service.Create("Boissons");
            _repository.SetProductCount(category.Id, 1);

            Assert.Throws<CategoryNotEmptyException>(() => _service.Delete(category.Id));
            Assert.Single(_repository.GetAll());
        }

        [Fact]
        public void Deleting_a_missing_category_is_refused()
        {
            Assert.Throws<BusinessRuleException>(() => _service.Delete(99));
        }
    }
}
