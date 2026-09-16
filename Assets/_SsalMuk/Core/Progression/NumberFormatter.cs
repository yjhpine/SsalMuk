using System;
using System.Globalization;
using System.Numerics;
namespace SsalMuk.Core
{
    public static class NumberFormatter
    {
        public static string Format(BigInteger value)
        {
            string digits = BigInteger.Abs(value).ToString(CultureInfo.InvariantCulture), sign = value.Sign < 0 ? "-" : "";
            return digits.Length <= 8 ? sign + digits : sign + digits[0] + "." + digits.Substring(1, 2) + "e" + (digits.Length - 1).ToString(CultureInfo.InvariantCulture);
        }
        public static double Fraction(BigInteger numerator, BigInteger denominator)
        {
            if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            if (numerator <= 0) return 0; if (numerator >= denominator) return 1;
            return (double)(numerator * 10000 / denominator) / 10000;
        }
    }
}
