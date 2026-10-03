using System.IO;
using Microsoft.Win32;

namespace Stokbox.App.Services
{
    public sealed class FileDialogService : IFileDialogService
    {
        public string ChooseFolder(string description)
        {
            // WPF has no folder picker on .NET Framework: the Windows Forms one is the standard Windows dialog.
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = true;

                return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        public string ChooseBackupFile(string initialDirectory)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choisir la sauvegarde à restaurer",
                Filter = "Sauvegardes Stokbox (*.db)|*.db|Tous les fichiers (*.*)|*.*",
                CheckFileExists = true
            };

            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
