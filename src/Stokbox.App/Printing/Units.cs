namespace Stokbox.App.Printing
{
    internal static class Units
    {
        private const double DipPerInch = 96.0;
        private const double MmPerInch = 25.4;

        /// <summary>
        /// Millimetres to WPF device-independent units (1/96 inch).
        /// </summary>
        public static double MmToDip(double millimetres)
        {
            return millimetres * DipPerInch / MmPerInch;
        }

        public static double MmToDip(decimal millimetres)
        {
            return MmToDip((double)millimetres);
        }
    }
}
