namespace Stokbox.Core.Repositories
{
    /// <summary>
    /// Persistent counter behind the internal barcodes.
    /// </summary>
    public interface IBarcodeSequence
    {
        /// <summary>
        /// Increments the counter atomically and returns the new value. A value is never returned twice.
        /// </summary>
        long Next();
    }
}
