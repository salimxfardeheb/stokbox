using System.Collections.Generic;
using System.Linq;
using Stokbox.App.Services;
using Stokbox.App.ViewModels;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;
using Xunit;

namespace Stokbox.App.Tests
{
    public class SecurityAndBackupScreensTests
    {
        private readonly MemorySettings _settings = new MemorySettings();
        private readonly ReceiptSettingsService _shopService;
        private readonly AuthService _auth;
        private readonly FakeBackups _backups = new FakeBackups();
        private readonly FakeFileDialogs _fileDialogs = new FakeFileDialogs();
        private readonly FakeDialogs _dialogs = new FakeDialogs();
        private readonly FakeRestarter _restarter = new FakeRestarter();

        public SecurityAndBackupScreensTests()
        {
            _shopService = new ReceiptSettingsService(_settings);
            _auth = new AuthService(_settings, _shopService);
        }

        // Premier lancement.

        [Fact]
        public void The_first_launch_shows_an_error_under_each_faulty_field_and_creates_nothing()
        {
            var setup = new SetupViewModel(_auth);

            Assert.False(setup.TryComplete("court", "court"));

            Assert.Equal("Le nom de la boutique est obligatoire.", setup.ShopNameError);
            Assert.Equal("Le mot de passe doit contenir au moins 8 caractères.", setup.PasswordError);
            Assert.Null(setup.ConfirmationError);
            Assert.False(_auth.IsConfigured);

            setup.ShopName = "Boutique El Baraka";
            Assert.False(setup.TryComplete("Boutique2026!", "Boutique2026"));
            Assert.Null(setup.ShopNameError);
            Assert.Null(setup.PasswordError);
            Assert.Equal("La confirmation ne correspond pas au mot de passe.", setup.ConfirmationError);
            Assert.False(_auth.IsConfigured);
        }

        [Fact]
        public void The_first_launch_saves_the_shop_and_the_password()
        {
            var setup = new SetupViewModel(_auth)
            {
                ShopName = "Boutique El Baraka",
                ShopAddress = "12 rue Didouche Mourad, Alger",
                ShopPhone = "021 00 00 00"
            };

            Assert.True(setup.TryComplete("Boutique2026!", "Boutique2026!"));

            Assert.True(_auth.IsConfigured);
            Assert.True(_auth.VerifyPassword("Boutique2026!"));
            Assert.Equal("Boutique El Baraka", _shopService.Get().ShopName);
            Assert.Equal("021 00 00 00", _shopService.Get().ShopPhone);
        }

        // Connexion.

        [Fact]
        public void Login_accepts_only_the_right_password()
        {
            Configure();
            var login = new LoginViewModel(_auth);

            Assert.False(login.TryLogin("mauvais"));
            Assert.Equal("Mot de passe incorrect.", login.Error);

            Assert.False(login.TryLogin(""));
            Assert.Equal("Saisissez le mot de passe.", login.Error);

            login.ClearError();
            Assert.Null(login.Error);

            Assert.True(login.TryLogin("Boutique2026!"));
            Assert.Null(login.Error);
        }

        // Paramètres > Sécurité.

        [Fact]
        public void Changing_the_password_requires_the_current_one_and_shows_the_errors()
        {
            Configure();
            var security = new SecurityViewModel(_auth);

            Assert.False(security.TryChangePassword("mauvais", "court", "court"));
            Assert.Equal("Le mot de passe actuel est incorrect.", security.CurrentPasswordError);
            Assert.Equal("Le mot de passe doit contenir au moins 8 caractères.", security.NewPasswordError);
            Assert.Null(security.StatusMessage);
            Assert.True(_auth.VerifyPassword("Boutique2026!"));

            Assert.False(security.TryChangePassword("Boutique2026!", "NouveauSecret9", "NouveauSecret8"));
            Assert.Null(security.CurrentPasswordError);
            Assert.Equal("La confirmation ne correspond pas au mot de passe.", security.ConfirmationError);

            Assert.True(security.TryChangePassword("Boutique2026!", "NouveauSecret9", "NouveauSecret9"));
            Assert.Null(security.ConfirmationError);
            Assert.NotNull(security.StatusMessage);
            Assert.True(_auth.VerifyPassword("NouveauSecret9"));
            Assert.False(_auth.VerifyPassword("Boutique2026!"));
        }

        // Paramètres > Boutique et Ticket.

        [Fact]
        public void The_shop_tab_and_the_receipt_tab_each_save_their_own_settings()
        {
            Configure();
            var shop = new ShopSettingsViewModel(_shopService);
            var receipt = new ReceiptSettingsViewModel(_shopService, new FakeReceipts(), new FakePrinters());
            shop.Load();
            receipt.Load();

            Assert.Equal(
                new[] { "(Aucune : ticket désactivé)", "Ticket 80 mm" },
                receipt.PrinterOptions.Select(o => o.Display));
            Assert.Null(receipt.SelectedPrinter.Name);

            receipt.SelectedPrinter = receipt.PrinterOptions[1];
            receipt.SaveCommand.Execute(null);

            shop.ShopName = "Nouvelle enseigne";
            shop.ShopPhone = "0550 00 00 00";
            shop.SaveCommand.Execute(null);

            var saved = _shopService.Get();
            Assert.Equal("Nouvelle enseigne", saved.ShopName);
            Assert.Equal("12 rue Didouche Mourad, Alger", saved.ShopAddress);
            Assert.Equal("0550 00 00 00", saved.ShopPhone);
            Assert.Equal("Ticket 80 mm", saved.PrinterName);

            // Back to "none": the receipt is disabled, the shop is untouched.
            receipt.SelectedPrinter = receipt.PrinterOptions[0];
            receipt.SaveCommand.Execute(null);
            Assert.Null(_shopService.Get().PrinterName);
            Assert.Equal("Nouvelle enseigne", _shopService.Get().ShopName);
        }

        [Fact]
        public void The_shop_name_cannot_be_emptied()
        {
            Configure();
            var shop = new ShopSettingsViewModel(_shopService);
            shop.Load();

            shop.ShopName = "  ";
            shop.SaveCommand.Execute(null);

            Assert.Equal("Le nom de la boutique est obligatoire.", shop.ShopNameError);
            Assert.Equal("Boutique El Baraka", _shopService.Get().ShopName);
        }

        // Paramètres > Sauvegarde.

        [Fact]
        public void A_manual_backup_goes_to_the_chosen_folder_and_reports_the_file()
        {
            var screen = CreateBackupScreen();
            _fileDialogs.Folder = @"E:\Sauvegardes";

            screen.BackupNowCommand.Execute(null);

            Assert.Equal(new[] { @"E:\Sauvegardes" }, _backups.BackupFolders);
            Assert.Equal(@"Sauvegarde créée et vérifiée : E:\Sauvegardes\stokbox_20261003_140507.db", screen.StatusMessage);
        }

        [Fact]
        public void Cancelling_the_folder_choice_backs_up_nothing()
        {
            var screen = CreateBackupScreen();
            _fileDialogs.Folder = null;

            screen.BackupNowCommand.Execute(null);

            Assert.Empty(_backups.BackupFolders);
            Assert.Null(screen.StatusMessage);
        }

        [Fact]
        public void A_failed_backup_is_reported()
        {
            var screen = CreateBackupScreen();
            _fileDialogs.Folder = @"E:\Sauvegardes";
            _backups.BackupFailure = new BusinessRuleException("La sauvegarde a échoué.");

            screen.BackupNowCommand.Execute(null);

            Assert.Equal(new[] { "La sauvegarde a échoué." }, _dialogs.Warnings);
            Assert.Null(screen.StatusMessage);
        }

        [Fact]
        public void A_file_that_cannot_be_restored_is_refused_before_any_confirmation()
        {
            var screen = CreateBackupScreen();
            _fileDialogs.File = @"E:\autre.db";
            _backups.CheckFailure = new BusinessRuleException("Ce fichier est corrompu ou n'est pas une sauvegarde Stokbox.");

            screen.RestoreCommand.Execute(null);

            Assert.Equal(new[] { "Ce fichier est corrompu ou n'est pas une sauvegarde Stokbox." }, _dialogs.Warnings);
            Assert.Empty(_dialogs.Confirmations);
            Assert.Empty(_backups.Restored);
            Assert.Equal(0, _restarter.Restarts);
        }

        [Fact]
        public void A_restoration_needs_a_confirmation()
        {
            var screen = CreateBackupScreen();
            _fileDialogs.File = @"E:\stokbox_20261001_180000.db";
            _dialogs.ConfirmAnswer = false;

            screen.RestoreCommand.Execute(null);

            Assert.Contains("stokbox_20261001_180000.db", _dialogs.Confirmations.Single());
            Assert.Empty(_backups.Restored);
            Assert.Equal(0, _restarter.Restarts);
        }

        [Fact]
        public void A_confirmed_restoration_replaces_the_database_then_restarts_the_application()
        {
            var screen = CreateBackupScreen();
            _fileDialogs.File = @"E:\stokbox_20261001_180000.db";
            _dialogs.ConfirmAnswer = true;

            screen.RestoreCommand.Execute(null);

            Assert.Equal(new[] { @"E:\stokbox_20261001_180000.db" }, _backups.Restored);
            Assert.Equal(1, _restarter.Restarts);
        }

        [Fact]
        public void A_failed_restoration_does_not_restart_the_application()
        {
            var screen = CreateBackupScreen();
            _fileDialogs.File = @"E:\stokbox_20261001_180000.db";
            _dialogs.ConfirmAnswer = true;
            _backups.RestoreFailure = new BusinessRuleException("La restauration a échoué.");

            screen.RestoreCommand.Execute(null);

            Assert.Equal(new[] { "La restauration a échoué." }, _dialogs.Warnings);
            Assert.Equal(0, _restarter.Restarts);
        }

        [Fact]
        public void Cancelling_the_file_choice_restores_nothing()
        {
            var screen = CreateBackupScreen();
            _fileDialogs.File = null;

            screen.RestoreCommand.Execute(null);

            Assert.Empty(_dialogs.Confirmations);
            Assert.Empty(_backups.Restored);
        }

        private void Configure()
        {
            _auth.CompleteFirstRun(new FirstRunInput
            {
                ShopName = "Boutique El Baraka",
                ShopAddress = "12 rue Didouche Mourad, Alger",
                ShopPhone = "021 00 00 00",
                Password = "Boutique2026!",
                PasswordConfirmation = "Boutique2026!"
            });
        }

        private BackupViewModel CreateBackupScreen()
        {
            return new BackupViewModel(_backups, _fileDialogs, _dialogs, _restarter);
        }

        private sealed class MemorySettings : ISettingsRepository
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

            public IReadOnlyDictionary<string, string> GetAll()
            {
                return new Dictionary<string, string>(_values);
            }

            public void Save(IReadOnlyDictionary<string, string> values)
            {
                foreach (var pair in values)
                {
                    _values[pair.Key] = pair.Value;
                }
            }
        }

        private sealed class FakeBackups : IBackupService
        {
            public List<string> BackupFolders { get; } = new List<string>();

            public List<string> Restored { get; } = new List<string>();

            public BusinessRuleException BackupFailure { get; set; }

            public BusinessRuleException CheckFailure { get; set; }

            public BusinessRuleException RestoreFailure { get; set; }

            public string BackupsDirectory => @"C:\ProgramData\Stokbox\backups";

            public string BackupTo(string directory)
            {
                if (BackupFailure != null)
                {
                    throw BackupFailure;
                }

                BackupFolders.Add(directory);
                return directory + @"\stokbox_20261003_140507.db";
            }

            public string BackupAutomatically()
            {
                return BackupTo(BackupsDirectory);
            }

            public void CheckRestorable(string filePath)
            {
                if (CheckFailure != null)
                {
                    throw CheckFailure;
                }
            }

            public string Restore(string filePath)
            {
                if (RestoreFailure != null)
                {
                    throw RestoreFailure;
                }

                Restored.Add(filePath);
                return BackupsDirectory + @"\stokbox_20261003_140508.db";
            }
        }

        private sealed class FakeFileDialogs : IFileDialogService
        {
            public string Folder { get; set; }

            public string File { get; set; }

            public string ChooseFolder(string description)
            {
                return Folder;
            }

            public string ChooseBackupFile(string initialDirectory)
            {
                return File;
            }
        }

        private sealed class FakeRestarter : IApplicationRestarter
        {
            public int Restarts { get; private set; }

            public void Restart()
            {
                Restarts++;
            }
        }

        private sealed class FakePrinters : IPrinterCatalog
        {
            public IReadOnlyList<string> GetPrinterNames()
            {
                return new[] { "Ticket 80 mm" };
            }
        }

        private sealed class FakeReceipts : IReceiptPrintService
        {
            public bool IsEnabled => false;

            public bool Print(Sale sale)
            {
                return false;
            }

            public bool PrintTest(ReceiptSettings settings)
            {
                return false;
            }
        }

        private sealed class FakeDialogs : IDialogService
        {
            public bool ConfirmAnswer { get; set; }

            public List<string> Confirmations { get; } = new List<string>();

            public List<string> Warnings { get; } = new List<string>();

            public bool Confirm(string message)
            {
                Confirmations.Add(message);
                return ConfirmAnswer;
            }

            public void ShowWarning(string message)
            {
                Warnings.Add(message);
            }

            public bool ShowReturn(ReturnViewModel viewModel)
            {
                return false;
            }

            public bool ShowPayment(PaymentViewModel viewModel)
            {
                return false;
            }

            public bool Ask(string headline, string question)
            {
                return false;
            }

            public bool ShowProductForm(ProductFormViewModel viewModel)
            {
                return false;
            }

            public void ShowCategories(CategoriesViewModel viewModel)
            {
            }
        }
    }
}
