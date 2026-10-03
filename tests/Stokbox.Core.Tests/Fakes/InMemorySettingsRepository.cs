using System.Collections.Generic;
using Stokbox.Core.Repositories;

namespace Stokbox.Core.Tests.Fakes
{
    public sealed class InMemorySettingsRepository : ISettingsRepository
    {
        public Dictionary<string, string> Values { get; } = new Dictionary<string, string>();

        public IReadOnlyDictionary<string, string> GetAll()
        {
            return new Dictionary<string, string>(Values);
        }

        public void Save(IReadOnlyDictionary<string, string> values)
        {
            foreach (var pair in values)
            {
                Values[pair.Key] = pair.Value;
            }
        }
    }
}
