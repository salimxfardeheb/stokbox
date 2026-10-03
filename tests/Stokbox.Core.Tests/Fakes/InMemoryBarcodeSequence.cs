using Stokbox.Core.Repositories;

namespace Stokbox.Core.Tests.Fakes
{
    public sealed class InMemoryBarcodeSequence : IBarcodeSequence
    {
        private long _lastValue;

        public InMemoryBarcodeSequence(long lastValue = 0)
        {
            _lastValue = lastValue;
        }

        public long Next()
        {
            return ++_lastValue;
        }
    }
}
