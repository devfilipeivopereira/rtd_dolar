using System;
using System.Globalization;

namespace ColetorProfitRTD.MarketData
{
    public static class ValueParser
    {
        private static readonly CultureInfo PtBr = new CultureInfo("pt-BR");
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static decimal? ToDecimal(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is decimal decimalValue)
            {
                return decimalValue;
            }

            if (value is double doubleValue)
            {
                return Convert.ToDecimal(doubleValue);
            }

            if (value is float floatValue)
            {
                return Convert.ToDecimal(floatValue);
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return longValue;
            }

            string text = value.ToString()?.Trim();

            if (string.IsNullOrWhiteSpace(text) || text == "-")
            {
                return null;
            }

            if (decimal.TryParse(text, NumberStyles.Any, PtBr, out decimal ptBrValue))
            {
                return ptBrValue;
            }

            if (decimal.TryParse(text, NumberStyles.Any, Invariant, out decimal invariantValue))
            {
                return invariantValue;
            }

            return null;
        }

        public static string ToText(object value)
        {
            return value == null ? null : value.ToString();
        }

        public static object ToJsonValue(object value)
        {
            decimal? number = ToDecimal(value);

            if (number.HasValue)
            {
                return number.Value;
            }

            return ToText(value);
        }
    }
}
