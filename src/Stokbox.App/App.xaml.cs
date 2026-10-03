using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Stokbox.App.Printing;
using Stokbox.App.Services;
using Stokbox.App.ViewModels;
using Stokbox.App.Views;
using Stokbox.Core;
using Stokbox.Core.Repositories;
using Stokbox.Core.Services;
using Stokbox.Data;
using Stokbox.Data.Migrations;
using Stokbox.Data.Repositories;

namespace Stokbox.App
{
    public partial class App : Application, IApplicationRestarter
    {
        private IErrorLog _errorLog;
        private ServiceProvider _services;

        // The closing backup only makes sense once the user is in, and not when restarting after a restoration
        // (the replaced database has just been backed up).
        private bool _isAuthenticated;
        private bool _isRestarting;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // The login window opens and closes before the main one exists: closing it must not end the application.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // The journal exists before anything else so that a failing startup is recorded too.
            var paths = AppPaths.CreateDefault();
            _errorLog = new FileErrorLog(paths.LogsDirectory);
            RegisterGlobalExceptionHandlers();

            try
            {
                _services = ConfigureServices(paths, _errorLog);
                _services.GetRequiredService<IDatabaseMigrator>().MigrateToLatest();
            }
            catch (Exception ex)
            {
                _errorLog.Write("Démarrage", ex);
                ShowError(
                    "Stokbox n'a pas pu démarrer : la base de données n'a pas pu être ouverte ou mise à jour.",
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            if (!Authenticate())
            {
                Shutdown(0);
                return;
            }

            _isAuthenticated = true;
            MainWindow = _services.GetRequiredService<MainWindow>();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_isAuthenticated && !_isRestarting)
            {
                BackupOnClose();
            }

            _services?.Dispose();
            base.OnExit(e);
        }

        public void Restart()
        {
            _isRestarting = true;
            Process.Start(Assembly.GetEntryAssembly().Location);
            Shutdown(0);
        }

        // First launch: the shop and the administrator password are created. Afterwards: the password is asked.
        private bool Authenticate()
        {
            var authService = _services.GetRequiredService<AuthService>();

            Window window = authService.IsConfigured
                ? (Window)new LoginWindow(new LoginViewModel(authService))
                : new SetupWindow(new SetupViewModel(authService));

            return window.ShowDialog() == true;
        }

        private void BackupOnClose()
        {
            try
            {
                _services.GetRequiredService<IBackupService>().BackupAutomatically();
            }
            catch (Exception ex)
            {
                // Closing must never be blocked by a failed backup; the journal keeps the reason.
                _errorLog.Write("Sauvegarde à la fermeture", ex);
            }
        }

        private ServiceProvider ConfigureServices(AppPaths paths, IErrorLog errorLog)
        {
            var services = new ServiceCollection();

            services.AddSingleton(paths);
            services.AddSingleton(errorLog);
            services.AddSingleton(new SqliteConnectionFactory(paths.DatabaseFilePath));
            services.AddSingleton(provider => new MigrationRunner(provider.GetRequiredService<SqliteConnectionFactory>()));
            services.AddSingleton<IDatabaseMigrator>(provider => provider.GetRequiredService<MigrationRunner>());
            services.AddSingleton<IBackupService>(provider => new SqliteBackupService(
                provider.GetRequiredService<SqliteConnectionFactory>(),
                paths.BackupsDirectory,
                provider.GetRequiredService<MigrationRunner>().LatestVersion));

            services.AddSingleton<ICategoryRepository, CategoryRepository>();
            services.AddSingleton<IProductRepository, ProductRepository>();
            services.AddSingleton<IBarcodeSequence, SqliteBarcodeSequence>();
            services.AddSingleton<BarcodeGenerator>();
            services.AddSingleton<CategoryService>();
            services.AddSingleton<ProductService>();
            services.AddSingleton<IStockMovementRepository, StockMovementRepository>();
            services.AddSingleton<StockService>();
            services.AddSingleton<ISettingsRepository, SettingsRepository>();
            services.AddSingleton<LabelSettingsService>();
            services.AddSingleton<ReceiptSettingsService>();
            services.AddSingleton<AuthService>();
            services.AddSingleton<ISaleRepository, SaleRepository>();
            services.AddSingleton<SaleService>(provider => new SaleService(
                provider.GetRequiredService<IProductRepository>(),
                provider.GetRequiredService<ISaleRepository>()));
            services.AddSingleton<IDashboardService, SqliteDashboardService>();

            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IFileDialogService, FileDialogService>();
            services.AddSingleton<IApplicationRestarter>(this);
            services.AddSingleton<IPrinterCatalog, WindowsPrinters>();
            services.AddSingleton<ILabelPrintService, LabelPrintService>();
            services.AddSingleton<IReceiptPrintService, ReceiptPrintService>();

            // One search per screen: each keeps its own text and results.
            services.AddTransient<ProductSearchViewModel>();
            services.AddSingleton<ProductsViewModel>();
            services.AddSingleton<StockEntriesViewModel>();
            services.AddSingleton<LabelsViewModel>();
            services.AddSingleton<LabelSettingsViewModel>();
            services.AddSingleton<ReceiptSettingsViewModel>();
            services.AddSingleton<ShopSettingsViewModel>();
            services.AddSingleton<SecurityViewModel>();
            services.AddSingleton<BackupViewModel>();
            services.AddSingleton<AboutViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<SaleViewModel>();
            services.AddSingleton<HistoryViewModel>();
            services.AddSingleton<DashboardViewModel>(provider => new DashboardViewModel(
                provider.GetRequiredService<IDashboardService>(),
                provider.GetRequiredService<IErrorLog>()));
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _errorLog.Write("Interface", e.Exception);
            ShowError(
                "Une erreur inattendue s'est produite. L'opération en cours a été interrompue.",
                MessageBoxImage.Warning);

            // Keep the till usable: the failed operation is abandoned, the application stays open.
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Raised on a non-UI thread; the process terminates after this handler returns.
            _errorLog.Write("Application", e.ExceptionObject as Exception);
            ShowError(
                "Une erreur inattendue s'est produite. Stokbox va se fermer.",
                MessageBoxImage.Error);
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            _errorLog.Write("Tâche de fond", e.Exception);
            e.SetObserved();
        }

        private void ShowError(string message, MessageBoxImage image)
        {
            MessageBox.Show(
                message + Environment.NewLine + Environment.NewLine
                    + "Le détail a été enregistré dans : " + _errorLog.LogsDirectory,
                "Stokbox",
                MessageBoxButton.OK,
                image);
        }
    }
}
