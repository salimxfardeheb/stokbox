using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.App.Services;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Settings > Backup: manual backup to a chosen folder, and restoration of a backup.
    /// </summary>
    public sealed class BackupViewModel : ObservableObject
    {
        private readonly IBackupService _backupService;
        private readonly IFileDialogService _fileDialogs;
        private readonly IDialogService _dialogs;
        private readonly IApplicationRestarter _restarter;

        private string _statusMessage;

        public BackupViewModel(
            IBackupService backupService,
            IFileDialogService fileDialogs,
            IDialogService dialogs,
            IApplicationRestarter restarter)
        {
            _backupService = backupService;
            _fileDialogs = fileDialogs;
            _dialogs = dialogs;
            _restarter = restarter;

            BackupNowCommand = new RelayCommand(BackupNow);
            RestoreCommand = new RelayCommand(Restore);
        }

        public RelayCommand BackupNowCommand { get; }

        public RelayCommand RestoreCommand { get; }

        /// <summary>
        /// Folder of the automatic backups, shown to the user.
        /// </summary>
        public string BackupsDirectory => _backupService.BackupsDirectory;

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Clears the messages; called each time the screen is shown.
        /// </summary>
        public void Load()
        {
            StatusMessage = null;
        }

        private void BackupNow()
        {
            var folder = _fileDialogs.ChooseFolder("Choisissez le dossier (ou la clé USB) où enregistrer la sauvegarde.");
            if (folder == null)
            {
                return;
            }

            try
            {
                var filePath = _backupService.BackupTo(folder);
                StatusMessage = "Sauvegarde créée et vérifiée : " + filePath;
            }
            catch (BusinessRuleException ex)
            {
                StatusMessage = null;
                _dialogs.ShowWarning(ex.Message);
            }
        }

        private void Restore()
        {
            var filePath = _fileDialogs.ChooseBackupFile(_backupService.BackupsDirectory);
            if (filePath == null)
            {
                return;
            }

            try
            {
                // Checked before asking: a file that cannot be restored must not even reach the confirmation.
                _backupService.CheckRestorable(filePath);
            }
            catch (BusinessRuleException ex)
            {
                _dialogs.ShowWarning(ex.Message);
                return;
            }

            var confirmed = _dialogs.Confirm(
                "Restaurer la sauvegarde « " + Path.GetFileName(filePath) + " » ?"
                + Environment.NewLine + Environment.NewLine
                + "Toutes les données actuelles (produits, stock, ventes, paramètres, mot de passe) seront remplacées par celles de la sauvegarde. "
                + "La base actuelle sera d'abord sauvegardée dans " + _backupService.BackupsDirectory + "."
                + Environment.NewLine + Environment.NewLine
                + "Stokbox redémarrera ensuite et demandera le mot de passe de la sauvegarde restaurée.");
            if (!confirmed)
            {
                return;
            }

            try
            {
                _backupService.Restore(filePath);
            }
            catch (BusinessRuleException ex)
            {
                _dialogs.ShowWarning(ex.Message);
                return;
            }

            // Every screen holds data of the replaced database: only a restart shows the restored one everywhere.
            _restarter.Restart();
        }
    }
}
