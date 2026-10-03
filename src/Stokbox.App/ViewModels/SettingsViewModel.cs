namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// The settings screen: one tab per topic.
    /// </summary>
    public sealed class SettingsViewModel
    {
        public SettingsViewModel(
            ShopSettingsViewModel shop,
            LabelSettingsViewModel labels,
            ReceiptSettingsViewModel receipt,
            SecurityViewModel security,
            BackupViewModel backup)
        {
            Shop = shop;
            Labels = labels;
            Receipt = receipt;
            Security = security;
            Backup = backup;
        }

        public ShopSettingsViewModel Shop { get; }

        public LabelSettingsViewModel Labels { get; }

        public ReceiptSettingsViewModel Receipt { get; }

        public SecurityViewModel Security { get; }

        public BackupViewModel Backup { get; }

        /// <summary>
        /// Fills the forms with the saved settings; called each time the screen is shown.
        /// </summary>
        public void Load()
        {
            Shop.Load();
            Labels.Load();
            Receipt.Load();
            Security.Load();
            Backup.Load();
        }
    }
}
