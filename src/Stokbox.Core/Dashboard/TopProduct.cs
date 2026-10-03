namespace Stokbox.Core.Dashboard
{
    public sealed class TopProduct
    {
        public TopProduct(string name, long netQuantity, long netRevenueCents)
        {
            Name = name;
            NetQuantity = netQuantity;
            NetRevenueCents = netRevenueCents;
        }

        public string Name { get; }

        public long NetQuantity { get; }

        public long NetRevenueCents { get; }
    }
}
