using System.Collections.Generic;

namespace Stokbox.Core.Repositories
{
    /// <summary>
    /// Key/value store of the application settings.
    /// </summary>
    public interface ISettingsRepository
    {
        IReadOnlyDictionary<string, string> GetAll();

        /// <summary>
        /// Writes the given values, all or none; the other keys are left untouched.
        /// </summary>
        void Save(IReadOnlyDictionary<string, string> values);
    }
}
