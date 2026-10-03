namespace Stokbox.App.ViewModels
{
    public enum DashboardPeriod
    {
        Today,
        Last7Days,
        CurrentMonth,
        PreviousMonth,
        Custom
    }

    /// <summary>
    /// One entry of the period selector of the dashboard.
    /// </summary>
    public sealed class DashboardPeriodOption
    {
        public DashboardPeriodOption(DashboardPeriod period, string name)
        {
            Period = period;
            Name = name;
        }

        public DashboardPeriod Period { get; }

        public string Name { get; }
    }
}
