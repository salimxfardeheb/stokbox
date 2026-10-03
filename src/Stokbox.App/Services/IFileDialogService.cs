namespace Stokbox.App.Services
{
    /// <summary>
    /// Windows dialogs to pick a folder or a file, on behalf of the ViewModels.
    /// </summary>
    public interface IFileDialogService
    {
        /// <returns>The folder chosen; null when the dialog is cancelled.</returns>
        string ChooseFolder(string description);

        /// <returns>The backup file chosen; null when the dialog is cancelled.</returns>
        string ChooseBackupFile(string initialDirectory);
    }
}
