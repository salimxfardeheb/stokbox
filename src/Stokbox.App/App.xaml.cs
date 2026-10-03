using System;
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
    public partial class App : Application
    {
        private IErrorLog _errorLog;
        private ServiceProvider _services;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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

            MainWindow = _services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _services?.Dispose();
            base.OnExit(e);
        }

        private static ServiceProvider ConfigureServices(AppPaths paths, IErrorLog errorLog)
        {
            var services = new ServiceCollection();

            services.AddSingleton(paths);
            services.AddSingleton(errorLog);
            services.AddSingleton(new SqliteConnectionFactory(paths.DatabaseFilePath));
            services.AddSingleton<IDatabaseMigrator>(
                provider => new MigrationRunner(provider.GetRequiredService<SqliteConnectionFactory>()));

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
            services.AddSingleton<ISaleRepository, SaleRepository>();
            services.AddSingleton<SaleService>(provider => new SaleService(
                provider.GetRequiredService<IProductRepository>(),
                provider.GetRequiredService<ISaleRepository>()));

            services.AddSingleton<IDialogService, DialogService>();
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
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<SaleViewModel>();
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
