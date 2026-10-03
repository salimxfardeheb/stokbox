using Stokbox.App.ViewModels;

namespace Stokbox.App.Services
{
    /// <summary>
    /// Opens the modal windows on behalf of the ViewModels.
    /// </summary>
    public interface IDialogService
    {
        /// <returns>True when the product was saved.</returns>
        bool ShowProductForm(ProductFormViewModel viewModel);

        void ShowCategories(CategoriesViewModel viewModel);

        bool Confirm(string message);

        void ShowWarning(string message);

        /// <returns>True when the sale was recorded.</returns>
        bool ShowPayment(PaymentViewModel viewModel);

        /// <summary>
        /// Yes/no question answered from the keyboard: Enter for yes, Escape for no.
        /// </summary>
        bool Ask(string headline, string question);
    }
}
