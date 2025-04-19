namespace Kokoro
{
    internal static class StringExtensions
    {
        public static string CapitalizeFirstLetter(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            return input[0].ToString().ToUpperInvariant() + input[1..];
        }
    }
}
