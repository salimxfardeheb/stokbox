namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// One entry of the printer list; a null name means "ask at each printing".
    /// </summary>
    public sealed class PrinterOption
    {
        public PrinterOption(string name, string display)
        {
            Name = name;
            Display = display;
        }

        public string Name { get; }

        public string Display { get; }
    }
}
