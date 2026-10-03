namespace Stokbox.Core.Entities
{
    /// <summary>
    /// Size of a price label and its arrangement on the printed page, in millimetres.
    /// Defaults suit a 50 × 30 mm roll in a thermal printer: one label per page.
    /// </summary>
    public sealed class LabelSettings
    {
        public decimal WidthMm { get; set; } = 50m;

        public decimal HeightMm { get; set; } = 30m;

        /// <summary>
        /// Blank band above the first row of labels, and below the last one.
        /// </summary>
        public decimal MarginTopMm { get; set; }

        /// <summary>
        /// Blank band left of the first column of labels, and right of the last one.
        /// </summary>
        public decimal MarginLeftMm { get; set; }

        /// <summary>
        /// Space between two columns of labels.
        /// </summary>
        public decimal HorizontalGapMm { get; set; }

        /// <summary>
        /// Space between two rows of labels.
        /// </summary>
        public decimal VerticalGapMm { get; set; }

        public int Columns { get; set; } = 1;

        public int Rows { get; set; } = 1;

        /// <summary>
        /// Windows name of the printer; null to ask at each printing.
        /// </summary>
        public string PrinterName { get; set; }

        public int LabelsPerPage => Columns * Rows;

        public decimal PageWidthMm => 2 * MarginLeftMm + Columns * WidthMm + (Columns - 1) * HorizontalGapMm;

        public decimal PageHeightMm => 2 * MarginTopMm + Rows * HeightMm + (Rows - 1) * VerticalGapMm;

        /// <summary>
        /// Distance from the left edge of the page to the labels of the given column (0-based).
        /// </summary>
        public decimal GetLabelLeftMm(int column)
        {
            return MarginLeftMm + column * (WidthMm + HorizontalGapMm);
        }

        /// <summary>
        /// Distance from the top edge of the page to the labels of the given row (0-based).
        /// </summary>
        public decimal GetLabelTopMm(int row)
        {
            return MarginTopMm + row * (HeightMm + VerticalGapMm);
        }

        public int CountPages(int labelCount)
        {
            return labelCount <= 0 ? 0 : (labelCount + LabelsPerPage - 1) / LabelsPerPage;
        }

        public LabelSettings Clone()
        {
            return (LabelSettings)MemberwiseClone();
        }
    }
}
