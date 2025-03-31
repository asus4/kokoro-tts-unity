using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Serialization.Json;

namespace Kokoro.Misaki
{
    internal class TokenContext
    {
        public bool? FutureVowel { get; set; } = null;
        public bool FutureTo { get; set; } = false;
    }

    public partial class EnglishG2P : IG2P
    {
        static readonly HashSet<char> DIPHTHONGS = new() { 'A', 'I', 'O', 'Q', 'W', 'Y', 'ʤ', 'ʧ' };
        static readonly HashSet<char> VOWELS = new() { 'A', 'I', 'O', 'Q', 'W', 'Y', 'a', 'i', 'u', 'æ', 'ɑ', 'ɒ', 'ɔ', 'ə', 'ɛ', 'ɜ', 'ɪ', 'ʊ', 'ʌ', 'ᵻ' };
        static readonly HashSet<char> CONSONANTS = new() { 'b', 'd', 'f', 'h', 'j', 'k', 'l', 'm', 'n', 'p', 's', 't', 'v', 'w', 'z', 'ð', 'ŋ', 'ɡ', 'ɹ', 'ɾ', 'ʃ', 'ʒ', 'ʤ', 'ʧ', 'θ' };
        static readonly HashSet<string> PUNCT_TAGS = new() { ".", ",", "-LRB-", "-RRB-", "``", "\"\"", "''", ":", "$", "#", "NFP" };
        static readonly Dictionary<string, string> PUNCT_TAG_PHONEMES = new() {
            { "-LRB-", "(" },
            { "-RRB-", ")" },
            { "``", "\u8220" },
            { "\"\"".ToString(), "\u8221" },
            { "''", "\u8221" }
        };

        public readonly LanguageCode LanguageCode;
        private readonly Lexicon lexicon;
        private readonly string unk;

        // Main constructor
        public EnglishG2P(LanguageCode lang, string unk = "❓")
        {
            this.unk = unk;
            LanguageCode = lang;
            lexicon = new Lexicon(lang == LanguageCode.En_GB);
        }

        public void Dispose()
        {
            // Clean up resources if needed
        }

        // Implementation of IG2P.Convert
        public string Convert(string text)
        {
            // 1. Preprocess the text
            var (preprocessedText, rawTokens, features) = Preprocess(text);

            // 2. Tokenize (simplified implementation as we don't have Spacy)
            var tokens = Tokenize(preprocessedText);

            // 3. Apply token conversion logic
            ApplyTokenConversion(tokens);

            // 4. Generate phonemes
            string phonemes = string.Join("", tokens.Select(tk => (tk.Phonemes ?? unk) + tk.WhiteSpace));
            return phonemes;
        }

        // Merge multiple tokens into one
        private MToken MergeTokens(List<MToken> tokens, string unk = null)
        {
            // Get unique stress values
            var stress = tokens
                .Where(tk => tk._.Stress != null)
                .Select(tk => tk._.Stress.Value)
                .Distinct()
                .ToList();

            // Get unique currency values
            var currency = tokens
                .Where(tk => tk._.Currency != null)
                .Select(tk => tk._.Currency)
                .Distinct()
                .ToList();

            // Get ratings
            var rating = tokens
                .Select(tk => tk._.Rating)
                .ToList();

            string phonemes = null;
            if (unk != null)
            {
                var phonemeBuilder = new StringBuilder();
                foreach (var tk in tokens)
                {
                    if (tk._.PreSpace && phonemeBuilder.Length > 0 &&
                        !char.IsWhiteSpace(phonemeBuilder[phonemeBuilder.Length - 1]) &&
                        tk.Phonemes != null)
                    {
                        phonemeBuilder.Append(" ");
                    }
                    phonemeBuilder.Append(tk.Phonemes == null ? unk : tk.Phonemes);
                }
                phonemes = phonemeBuilder.ToString();
            }

            // Build the combined token text
            var textBuilder = new StringBuilder();
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                textBuilder.Append(tokens[i].Text);
                textBuilder.Append(tokens[i].WhiteSpace);
            }
            textBuilder.Append(tokens.Last().Text);

            // Find the tag with the highest priority
            var tag = tokens.OrderByDescending(tk =>
                tk.Text.Sum(c => char.IsLower(c) ? 1 : 2)).First().Tag;

            return new MToken(
                textBuilder.ToString(),
                tag,
                tokens.Last().WhiteSpace,
                phonemes,
                tokens.First().StartTS,
                tokens.Last().EndTS)
            {
                _ = new UnderscoreData
                {
                    IsHead = tokens.First()._.IsHead,
                    Alias = null,
                    Stress = stress.Count == 1 ? stress[0] : null,
                    Currency = currency.Any() ? currency.Max() : null,
                    NumFlags = string.Join("", tokens
                        .SelectMany(tk => tk._.NumFlags)
                        .Distinct()
                        .OrderBy(c => c)),
                    PreSpace = tokens.First()._.PreSpace,
                    Rating = rating.Contains(null) ? null : rating.Min()
                }
            };
        }

        // Calculate stress weight
        private int StressWeight(string phonemes)
        {
            if (string.IsNullOrEmpty(phonemes)) return 0;

            return phonemes.Sum(c => DIPHTHONGS.Contains(c) ? 2 : 1);
        }

        // Preprocess text
        internal static (string, List<string>, Dictionary<int, object>) Preprocess(string text)
        {
            // Simple implementation - in a real app, you'd parse Markdown links, etc.
            return (text, text.Split(' ').ToList(), new Dictionary<int, object>());
        }

        // Tokenize text (simplified)
        private List<MToken> Tokenize(string text)
        {
            // Simplified tokenization - in a real app, you'd use a proper NLP library
            return text.Split(' ')
                .Select(t => new MToken(
                    t,
                    DetermineTag(t),
                    " ",
                    null))
                .ToList();
        }

        // Simple tag determination
        private string DetermineTag(string token)
        {
            // This is a very simplified implementation
            if (Regex.IsMatch(token, @"^\d+$")) return "CD";
            if (token.All(c => char.IsPunctuation(c))) return ".";
            return "NN";
        }

        // Apply token conversion
        private void ApplyTokenConversion(List<MToken> tokens)
        {
            var ctx = new TokenContext();

            foreach (var token in tokens)
            {
                if (token.Phonemes == null)
                {
                    var (phonemes, rating) = lexicon.GetWord(token, token.Tag, token._.Stress, ctx);
                    token.Phonemes = phonemes;
                    token._.Rating = rating;
                }

                // Update context for next iteration
                ctx = UpdateContext(ctx, token.Phonemes, token);
            }
        }

        // Update token context
        private TokenContext UpdateContext(TokenContext ctx, string phonemes, MToken token)
        {
            bool? vowel = ctx.FutureVowel;

            if (!string.IsNullOrEmpty(phonemes))
            {
                foreach (char c in phonemes)
                {
                    if (VOWELS.Contains(c))
                    {
                        vowel = true;
                        break;
                    }
                    if (CONSONANTS.Contains(c))
                    {
                        vowel = false;
                        break;
                    }
                }
            }

            bool futureTo = token.Text.ToLower() == "to" ||
                           (token.Text == "TO" && (token.Tag == "TO" || token.Tag == "IN"));

            return new TokenContext
            {
                FutureVowel = vowel,
                FutureTo = futureTo
            };
        }


    }
}
