namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// The settings screen: one tab per topic.
    /// </summary>
    public sealed class SettingsViewModel
    {
        public SettingsViewModel(LabelSettingsViewModel labels, ReceiptSettingsViewModel receipt)
        {
            Labels = labels;
            Receipt = receipt;
        }

        public LabelSettingsViewModel Labels { get; }

        public ReceiptSettingsViewModel Receipt { get; }

        /// <summary>
        /// Fills the forms with the saved settings; called each time the screen is shown.
        /// </summary>
        public void Load()
        {
            Labels.Load();
            Receipt.Load();
        }
    }
}
