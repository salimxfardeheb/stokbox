using System.Windows;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// One day of the "net revenue per day" chart. The four heights are shares of the plot area:
    /// the gap above the bar, the bar above the zero line, the bar below it, the gap below.
    /// They add up to the same total for every day, so the zero lines are aligned.
    /// </summary>
    public sealed class DashboardBar
    {
        public DashboardBar(string label, string toolTipText, double topGap, double positive, double negative, double bottomGap)
        {
            Label = label;
            ToolTipText = toolTipText;
            TopGap = new GridLength(topGap, GridUnitType.Star);
            Positive = new GridLength(positive, GridUnitType.Star);
            Negative = new GridLength(negative, GridUnitType.Star);
            BottomGap = new GridLength(bottomGap, GridUnitType.Star);
        }

        /// <summary>
        /// Day shown under the bar; empty when the period is too long for one label per day.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Date and amount, shown when the mouse is over the day.
        /// </summary>
        public string ToolTipText { get; }

        public GridLength TopGap { get; }

        public GridLength Positive { get; }

        public GridLength Negative { get; }

        public GridLength BottomGap { get; }
    }

    /// <summary>
    /// One line of the "top products" table.
    /// </summary>
    public sealed class DashboardProductRow
    {
        public DashboardProductRow(string name, string quantityText, string revenueText)
        {
            Name = name;
            QuantityText = quantityText;
            RevenueText = revenueText;
        }

        public string Name { get; }

        public string QuantityText { get; }

        public string RevenueText { get; }
    }

    /// <summary>
    /// One line of the "revenue per category" table.
    /// </summary>
    public sealed class DashboardCategoryRow
    {
        public DashboardCategoryRow(string name, string revenueText, string shareText)
        {
            Name = name;
            RevenueText = revenueText;
            ShareText = shareText;
        }

        public string Name { get; }

        public string RevenueText { get; }

        /// <summary>
        /// Share of the net revenue of the period: "23,4 %", or "—" when that revenue is not positive.
        /// </summary>
        public string ShareText { get; }
    }
}
