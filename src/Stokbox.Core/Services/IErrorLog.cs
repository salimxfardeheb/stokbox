using System;

namespace Stokbox.Core.Services
{
    /// <summary>
    /// Journal of unexpected errors. Implementations must never throw.
    /// </summary>
    public interface IErrorLog
    {
        /// <summary>
        /// Directory the journal is written to (shown to the user in error messages).
        /// </summary>
        string LogsDirectory { get; }

        void Write(string context, Exception exception);
    }
}
