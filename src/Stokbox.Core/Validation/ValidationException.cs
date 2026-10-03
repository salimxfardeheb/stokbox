using System;
using System.Collections.Generic;

namespace Stokbox.Core.Validation
{
    /// <summary>
    /// Invalid input, with one message per faulty field.
    /// </summary>
    public sealed class ValidationException : BusinessRuleException
    {
        public ValidationException(IReadOnlyList<ValidationError> errors)
            : base(FirstMessage(errors))
        {
            Errors = errors;
        }

        public ValidationException(string field, string message)
            : this(new[] { new ValidationError(field, message) })
        {
        }

        public IReadOnlyList<ValidationError> Errors { get; }

        private static string FirstMessage(IReadOnlyList<ValidationError> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                throw new ArgumentException("Au moins une erreur est attendue.", nameof(errors));
            }

            return errors[0].Message;
        }
    }
}
