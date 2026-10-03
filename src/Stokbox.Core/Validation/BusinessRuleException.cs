using System;

namespace Stokbox.Core.Validation
{
    /// <summary>
    /// An operation refused by a business rule. The message is in French and can be shown to the user as is.
    /// </summary>
    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string message)
            : base(message)
        {
        }
    }
}
