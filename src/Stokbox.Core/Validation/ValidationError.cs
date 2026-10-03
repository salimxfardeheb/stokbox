namespace Stokbox.Core.Validation
{
    public sealed class ValidationError
    {
        public ValidationError(string field, string message)
        {
            Field = field;
            Message = message;
        }

        /// <summary>
        /// Name of the input field the message belongs to.
        /// </summary>
        public string Field { get; }

        public string Message { get; }
    }
}
