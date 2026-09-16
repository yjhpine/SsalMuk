using System;
namespace SsalMuk.Core
{
    public sealed class NumericRangeException : ArithmeticException
    {
        public NumericRangeException(string message) : base(message) { }
    }
}
