using System;
using Catalyst;

namespace Kokoro
{
    internal static class CatalystExtensions
    {
        public static bool TryGetLemma(this IToken token, out string lemma, out string stem)
        {
            var lemmaSpan = token.LemmaAsSpan;
            var valueSpan = token.ValueAsSpan;
            if (lemmaSpan.SequenceEqual(valueSpan))
            {
                lemma = string.Empty;
                stem = string.Empty;
                return false;
            }
            lemma = lemmaSpan.ToString();
            // Get Stem part from lemma
            int minLength = Math.Min(lemma.Length, valueSpan.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (valueSpan[i] != lemmaSpan[i])
                {
                    stem = valueSpan[i..].ToString();
                    return true;
                }
            }
            if (lemma.Length < valueSpan.Length)
            {
                stem = valueSpan[lemma.Length..].ToString();
                return true;
            }
            throw new ArgumentException($"Invalid lemma: {token.Lemma} for token: {token.Value}");
        }

        public static string ToDebugString(this IToken token)
        {
            return $"(word:{token.Value}, lemma:{token.Lemma}, post:{token.POS}, replacement:{token.Replacement}, dependency:{token.DependencyType}, frequency:{token.Frequency})";
        }
    }
}
