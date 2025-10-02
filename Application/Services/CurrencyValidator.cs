namespace Application.Services
{
    public static class CurrencyValidator
    {
        private static readonly HashSet<string> Iso4217Codes = new(StringComparer.OrdinalIgnoreCase)
        {
            "USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD", "CNY", "RUB", "SEK", "NOK", "DKK", "PLN", "CZK", "HUF",
            "KZT", "UAH",
            "TRY", "BRL", "MXN", "INR", "KRW", "HKD", "SGD", "ZAR", "NZD", "AED", "SAR", "ILS", "THB", "MYR", "IDR"
        };

        public static bool IsValid(string code) => Iso4217Codes.Contains(code);
    }
}