using System;
using System.Data.SQLite;
using System.Globalization;
using Stokbox.Core;

namespace Stokbox.Data
{
    /// <summary>
    /// SQL function fold(text): the text without accents, in upper case.
    /// SQLite alone only ignores the case of ASCII letters and knows nothing about accents.
    /// </summary>
    internal sealed class FoldFunction : SQLiteFunction
    {
        public const string SqlName = "fold";

        public static void Bind(SQLiteConnection connection)
        {
            var attribute = new SQLiteFunctionAttribute
            {
                Name = SqlName,
                Arguments = 1,
                FuncType = FunctionType.Scalar
            };
            connection.BindFunction(attribute, new FoldFunction());
        }

        public override object Invoke(object[] args)
        {
            if (args[0] == null || args[0] is DBNull)
            {
                return DBNull.Value;
            }

            return TextNormalizer.Fold(Convert.ToString(args[0], CultureInfo.InvariantCulture));
        }
    }
}
