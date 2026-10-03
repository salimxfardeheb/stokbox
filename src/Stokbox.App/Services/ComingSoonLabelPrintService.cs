using Stokbox.Core.Entities;
using Stokbox.Core.Services;

namespace Stokbox.App.Services
{
    /// <summary>
    /// Placeholder until label printing exists: prints nothing and says so.
    /// </summary>
    public sealed class ComingSoonLabelPrintService : ILabelPrintService
    {
        private readonly IDialogService _dialogs;

        public ComingSoonLabelPrintService(IDialogService dialogs)
        {
            _dialogs = dialogs;
        }

        public void PrintLabels(Product product, int copies)
        {
            _dialogs.ShowInformation("Impression des étiquettes : bientôt disponible.");
        }
    }
}
