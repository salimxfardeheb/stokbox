using System;
using System.Globalization;
using Stokbox.Core.Repositories;

namespace Stokbox.Core.Services
{
    /// <summary>
    /// Internal barcodes: EAN-13 made of the prefix "20" (range reserved for in-store use),
    /// a 10-digit sequential number and the check digit.
    /// </summary>
    public sealed class BarcodeGenerator
    {
        public const string Prefix = "20";
        public const long MaxSequenceValue = 9999999999L;

        private readonly IBarcodeSequence _sequence;

        public BarcodeGenerator(IBarcodeSequence sequence)
        {
            _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        }

        /// <summary>
        /// Takes the next number of the sequence and returns its barcode.
        /// </summary>
        public string Next()
        {
            return FromSequenceValue(_sequence.Next());
        }

        public static string FromSequenceValue(long value)
        {
            if (value < 1 || value > MaxSequenceValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Le numéro de séquence du code-barres doit être compris entre 1 et 9 999 999 999.");
            }

            var body = Prefix + value.ToString("D10", CultureInfo.InvariantCulture);
            return body + ComputeCheckDigit(body);
        }

        /// <summary>
        /// EAN-13 check digit of the first 12 digits.
        /// </summary>
        public static char ComputeCheckDigit(string first12Digits)
        {
            if (first12Digits == null || first12Digits.Length != 12 || !IsAllDigits(first12Digits))
            {
                throw new ArgumentException("12 chiffres sont attendus.", nameof(first12Digits));
            }

            return (char)('0' + CheckDigitOf(first12Digits));
        }

        public static bool IsValidEan13(string code)
        {
            return code != null
                && code.Length == 13
                && IsAllDigits(code)
                && code[12] - '0' == CheckDigitOf(code);
        }

        // Weights 1 and 3 alternate from the leftmost digit; only the first 12 digits are read.
        private static int CheckDigitOf(string digits)
        {
            var sum = 0;
            for (var i = 0; i < 12; i++)
            {
                var digit = digits[i] - '0';
                sum += i % 2 == 0 ? digit : digit * 3;
            }

            return (10 - sum % 10) % 10;
        }

        private static bool IsAllDigits(string text)
        {
            foreach (var c in text)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
