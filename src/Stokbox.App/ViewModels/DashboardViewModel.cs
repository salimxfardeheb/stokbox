using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.Core;
using Stokbox.Core.Dashboard;
using Stokbox.Core.Services;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Dashboard screen: value of the stock and sales indicators of a period. Read-only.
    /// The figures are computed off the interface thread, when the screen is shown, when the period
    /// changes and on "Actualiser"; never on a timer.
    /// </summary>
    public sealed class DashboardViewModel : ObservableObject
    {
        private const string NoValue = "—";
        private const string DateFormat = "dd/MM/yyyy";

        // One label per day up to a month; beyond, only the ends of the period are named.
        private const int MaxDaysWithLabel = 31;
        private const int MaxDaysWithFullLabel = 16;

        private static readonly NumberFormatInfo FrenchNumbers = new NumberFormatInfo
        {
            NumberDecimalSeparator = ",",
            NumberGroupSeparator = " ",
            NumberGroupSizes = new[] { 3 }
        };

        private readonly IDashboardService _dashboard;
        private readonly IErrorLog _errorLog;
        private readonly Func<DateTime> _today;

        private DashboardPeriodOption _selectedPeriod;
        private DateTime? _customFrom;
        private DateTime? _customTo;
        private bool _isLoading;
        private string _errorMessage;
        private string _periodText;

        private string _stockPurchaseValueText = NoValue;
        private string _stockSaleValueText = NoValue;
        private string _stockMarginText = NoValue;
        private string _stockReferenceCountText = NoValue;
        private string _stockUnitCountText = NoValue;
        private string _outOfStockCountText = NoValue;

        private string _netRevenueText = NoValue;
        private string _grossProfitText = NoValue;
        private string _marginRateText = NoValue;
        private string _saleCountText = NoValue;
        private string _averageBasketText = NoValue;
        private string _netItemCountText = NoValue;

        private string _chartMaxText;
        private string _chartMinText;
        private string _chartFirstDayText;
        private string _chartLastDayText;

        // Only the latest request is displayed: an older one finishing late is ignored.
        private int _loadVersion;

        public DashboardViewModel(IDashboardService dashboard, IErrorLog errorLog, Func<DateTime> today = null)
        {
            _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
            _errorLog = errorLog;
            _today = today ?? (() => DateTime.Today);

            Periods = new[]
            {
                new DashboardPeriodOption(DashboardPeriod.Today, "Aujourd'hui"),
                new DashboardPeriodOption(DashboardPeriod.Last7Days, "7 derniers jours"),
                new DashboardPeriodOption(DashboardPeriod.CurrentMonth, "Mois en cours"),
                new DashboardPeriodOption(DashboardPeriod.PreviousMonth, "Mois précédent"),
                new DashboardPeriodOption(DashboardPeriod.Custom, "Période personnalisée")
            };
            _selectedPeriod = Periods[0];
            _customFrom = _today().Date;
            _customTo = _today().Date;

            DailyBars = new ObservableCollection<DashboardBar>();
            TopProducts = new ObservableCollection<DashboardProductRow>();
            Categories = new ObservableCollection<DashboardCategoryRow>();

            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            ShowOutOfStockCommand = new RelayCommand(() => OutOfStockRequested?.Invoke(this, EventArgs.Empty));
        }

        /// <summary>
        /// Raised when the user asks for the list of the products out of stock.
        /// </summary>
        public event EventHandler OutOfStockRequested;

        public IReadOnlyList<DashboardPeriodOption> Periods { get; }

        public ObservableCollection<DashboardBar> DailyBars { get; }

        public ObservableCollection<DashboardProductRow> TopProducts { get; }

        public ObservableCollection<DashboardCategoryRow> Categories { get; }

        public AsyncRelayCommand RefreshCommand { get; }

        public RelayCommand ShowOutOfStockCommand { get; }

        public DashboardPeriodOption SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (value != null && SetProperty(ref _selectedPeriod, value))
                {
                    OnPropertyChanged(nameof(IsCustomPeriod));
                    Refresh();
                }
            }
        }

        public bool IsCustomPeriod => _selectedPeriod.Period == DashboardPeriod.Custom;

        public DateTime? CustomFrom
        {
            get => _customFrom;
            set
            {
                if (SetProperty(ref _customFrom, value) && IsCustomPeriod)
                {
                    Refresh();
                }
            }
        }

        public DateTime? CustomTo
        {
            get => _customTo;
            set
            {
                if (SetProperty(ref _customTo, value) && IsCustomPeriod)
                {
                    Refresh();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// The period the sales figures shown were computed for: "Du 01/10/2026 au 03/10/2026".
        /// </summary>
        public string PeriodText
        {
            get => _periodText;
            private set => SetProperty(ref _periodText, value);
        }

        public string StockPurchaseValueText
        {
            get => _stockPurchaseValueText;
            private set => SetProperty(ref _stockPurchaseValueText, value);
        }

        public string StockSaleValueText
        {
            get => _stockSaleValueText;
            private set => SetProperty(ref _stockSaleValueText, value);
        }

        public string StockMarginText
        {
            get => _stockMarginText;
            private set => SetProperty(ref _stockMarginText, value);
        }

        public string StockReferenceCountText
        {
            get => _stockReferenceCountText;
            private set => SetProperty(ref _stockReferenceCountText, value);
        }

        public string StockUnitCountText
        {
            get => _stockUnitCountText;
            private set => SetProperty(ref _stockUnitCountText, value);
        }

        public string OutOfStockCountText
        {
            get => _outOfStockCountText;
            private set => SetProperty(ref _outOfStockCountText, value);
        }

        public string NetRevenueText
        {
            get => _netRevenueText;
            private set => SetProperty(ref _netRevenueText, value);
        }

        public string GrossProfitText
        {
            get => _grossProfitText;
            private set => SetProperty(ref _grossProfitText, value);
        }

        public string MarginRateText
        {
            get => _marginRateText;
            private set => SetProperty(ref _marginRateText, value);
        }

        public string SaleCountText
        {
            get => _saleCountText;
            private set => SetProperty(ref _saleCountText, value);
        }

        public string AverageBasketText
        {
            get => _averageBasketText;
            private set => SetProperty(ref _averageBasketText, value);
        }

        public string NetItemCountText
        {
            get => _netItemCountText;
            private set => SetProperty(ref _netItemCountText, value);
        }

        /// <summary>
        /// Amount at the top of the chart: the best day of the period.
        /// </summary>
        public string ChartMaxText
        {
            get => _chartMaxText;
            private set => SetProperty(ref _chartMaxText, value);
        }

        /// <summary>
        /// Amount at the bottom of the chart; null unless a day has more returns than sales.
        /// </summary>
        public string ChartMinText
        {
            get => _chartMinText;
            private set => SetProperty(ref _chartMinText, value);
        }

        /// <summary>
        /// Ends of the period, shown under the chart when the days are too many to be labelled one by one.
        /// </summary>
        public string ChartFirstDayText
        {
            get => _chartFirstDayText;
            private set => SetProperty(ref _chartFirstDayText, value);
        }

        public string ChartLastDayText
        {
            get => _chartLastDayText;
            private set => SetProperty(ref _chartLastDayText, value);
        }

        public bool HasNoTopProducts => TopProducts.Count == 0;

        public bool HasNoCategories => Categories.Count == 0;

        /// <summary>
        /// Starts computing the figures again; called each time the screen is shown.
        /// </summary>
        public void Refresh()
        {
            // Failures are handled inside: the task never faults.
            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            var version = ++_loadVersion;
            var period = CurrentPeriod();

            IsLoading = true;
            ErrorMessage = null;
            try
            {
                var snapshot = await Task.Run(() => Snapshot.Load(_dashboard, period.Item1, period.Item2));
                if (version != _loadVersion)
                {
                    return;
                }

                Show(snapshot);
            }
            catch (Exception ex)
            {
                if (version == _loadVersion)
                {
                    _errorLog?.Write("Tableau de bord", ex);
                    ErrorMessage = "Les indicateurs n'ont pas pu être calculés. Réessayez avec « Actualiser ».";
                }
            }
            finally
            {
                if (version == _loadVersion)
                {
                    IsLoading = false;
                }
            }
        }

        // Whole local days, both included.
        private Tuple<DateTime, DateTime> CurrentPeriod()
        {
            var today = _today().Date;
            var firstOfMonth = new DateTime(today.Year, today.Month, 1);

            switch (_selectedPeriod.Period)
            {
                case DashboardPeriod.Last7Days:
                    return Tuple.Create(today.AddDays(-6), today);
                case DashboardPeriod.CurrentMonth:
                    return Tuple.Create(firstOfMonth, today);
                case DashboardPeriod.PreviousMonth:
                    return Tuple.Create(firstOfMonth.AddMonths(-1), firstOfMonth.AddDays(-1));
                case DashboardPeriod.Custom:
                    // An emptied date field means "today"; dates given in the wrong order are swapped.
                    var from = (CustomFrom ?? today).Date;
                    var to = (CustomTo ?? today).Date;
                    return from <= to ? Tuple.Create(from, to) : Tuple.Create(to, from);
                default:
                    return Tuple.Create(today, today);
            }
        }

        private void Show(Snapshot snapshot)
        {
            var stock = snapshot.Stock;
            StockPurchaseValueText = Money.Format(stock.PurchaseValueCents);
            StockSaleValueText = Money.Format(stock.SaleValueCents);
            StockMarginText = Money.Format(stock.PotentialMarginCents);
            StockReferenceCountText = FormatCount(stock.ReferenceCount);
            StockUnitCountText = FormatCount(stock.UnitCount);
            OutOfStockCountText = FormatCount(stock.OutOfStockCount);

            var kpis = snapshot.Kpis;
            NetRevenueText = Money.Format(kpis.NetRevenueCents);
            GrossProfitText = Money.Format(kpis.GrossProfitCents);
            MarginRateText = kpis.MarginRatePercent.HasValue ? FormatPercent(kpis.MarginRatePercent.Value) : NoValue;
            SaleCountText = FormatCount(kpis.SaleCount);
            AverageBasketText = kpis.AverageBasketCents.HasValue ? Money.Format(kpis.AverageBasketCents.Value) : NoValue;
            NetItemCountText = FormatCount(kpis.NetItemCount);

            PeriodText = snapshot.From == snapshot.To
                ? "Le " + FormatDate(snapshot.From)
                : "Du " + FormatDate(snapshot.From) + " au " + FormatDate(snapshot.To);

            ShowChart(snapshot.Days);

            TopProducts.Clear();
            foreach (var product in snapshot.TopProducts)
            {
                TopProducts.Add(new DashboardProductRow(
                    product.Name,
                    FormatCount(product.NetQuantity),
                    Money.Format(product.NetRevenueCents)));
            }

            Categories.Clear();
            foreach (var category in snapshot.Categories)
            {
                Categories.Add(new DashboardCategoryRow(
                    category.CategoryName ?? "Sans catégorie",
                    Money.Format(category.NetRevenueCents),
                    kpis.NetRevenueCents > 0
                        ? FormatPercent(Math.Round(category.NetRevenueCents * 100m / kpis.NetRevenueCents, 1, MidpointRounding.AwayFromZero))
                        : NoValue));
            }

            OnPropertyChanged(nameof(HasNoTopProducts));
            OnPropertyChanged(nameof(HasNoCategories));
        }

        private void ShowChart(IReadOnlyList<DailySales> days)
        {
            // Heights above and below the zero line, the same for every day. A period without any
            // amount still gets a plot area, with empty bars.
            var highest = Math.Max(0, days.Count == 0 ? 0 : days.Max(day => day.NetRevenueCents));
            var lowest = Math.Min(0, days.Count == 0 ? 0 : days.Min(day => day.NetRevenueCents));
            double above = highest;
            double below = -lowest;
            if (above + below <= 0)
            {
                above = 1;
            }

            var total = above + below;

            DailyBars.Clear();
            foreach (var day in days)
            {
                double positive = Math.Max(0, day.NetRevenueCents);
                double negative = Math.Max(0, -day.NetRevenueCents);
                DailyBars.Add(new DashboardBar(
                    DayLabel(day.Date, days.Count),
                    FormatDate(day.Date) + " — " + Money.Format(day.NetRevenueCents),
                    (above - positive) / total,
                    positive / total,
                    negative / total,
                    (below - negative) / total));
            }

            ChartMaxText = Money.Format(highest);
            ChartMinText = lowest < 0 ? Money.Format(lowest) : null;

            var endsOnly = days.Count > MaxDaysWithLabel;
            ChartFirstDayText = endsOnly ? FormatDate(days[0].Date) : null;
            ChartLastDayText = endsOnly ? FormatDate(days[days.Count - 1].Date) : null;
        }

        private static string DayLabel(DateTime day, int dayCount)
        {
            if (dayCount > MaxDaysWithLabel)
            {
                return string.Empty;
            }

            return day.ToString(dayCount > MaxDaysWithFullLabel ? "dd" : "dd/MM", CultureInfo.InvariantCulture);
        }

        private static string FormatDate(DateTime date)
        {
            return date.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        private static string FormatCount(long count)
        {
            return count.ToString("#,0", FrenchNumbers);
        }

        private static string FormatPercent(decimal percent)
        {
            return percent.ToString("0.0", FrenchNumbers) + " %";
        }

        /// <summary>
        /// Everything the screen shows, read in one go off the interface thread.
        /// </summary>
        private sealed class Snapshot
        {
            public DateTime From { get; private set; }

            public DateTime To { get; private set; }

            public StockSummary Stock { get; private set; }

            public SalesKpis Kpis { get; private set; }

            public IReadOnlyList<TopProduct> TopProducts { get; private set; }

            public IReadOnlyList<CategorySales> Categories { get; private set; }

            public IReadOnlyList<DailySales> Days { get; private set; }

            public static Snapshot Load(IDashboardService dashboard, DateTime from, DateTime to)
            {
                return new Snapshot
                {
                    From = from,
                    To = to,
                    Stock = dashboard.GetStockSummary(),
                    Kpis = dashboard.GetSalesKpis(from, to),
                    TopProducts = dashboard.GetTopProducts(from, to),
                    Categories = dashboard.GetSalesByCategory(from, to),
                    Days = dashboard.GetDailySales(from, to)
                };
            }
        }
    }
}
