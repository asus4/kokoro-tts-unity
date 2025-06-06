using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Catalyst;
using Mosaik.Core;
using System.Threading.Tasks;
using System.Threading;

namespace Kokoro.Misaki
{
    #region Constants
    internal static class Constants
    {
        public static readonly Regex LinkRegex = new(@"\[([^\]]+)\]\(([^\)]*)\)");

        public static readonly HashSet<char> SubtokenJunks = new("',-._‘’/");
        public static readonly HashSet<char> Puncts = new(";:,.!?—…\"“”");
        public static readonly HashSet<char> NonQuotePuncts = new(";:,.!?—…");


        public static readonly HashSet<char> UsTaus = new("AIOWYiuæɑəɛɪɹʊʌ");
        public static readonly HashSet<char> Vowels = new("AIOQWYaiuæɑɒɔəɛɜɪʊʌᵻ");
        public static readonly HashSet<char> Consonants = new("bdfhjklmnpstvwzðŋɡɹɾʃʒʤʧθ");
        public static readonly HashSet<string> Ordinals = new() { "st", "nd", "rd", "th" };
        public static readonly Dictionary<string, Tuple<string, string>> Currencies = new()
        {
            { "$", Tuple.Create("dollar", "cent") },
            { "£", Tuple.Create("pound", "pence") },
            { "€", Tuple.Create("euro", "cent") }
        };
    }
    #endregion // Constants

    #region MToken
    internal record MToken
    {
        public string Text { get; set; }
        public string Tag { get; set; }
        public string Whitespace { get; set; }
        public string Phonemes { get; set; }
        public double? StartTs { get; set; }
        public double? EndTs { get; set; }
        public Underscore _ { get; set; }

        public MToken(string text, string tag, string whitespace, string phonemes = null,
                      double? startTs = null, double? endTs = null, Underscore underscore = null)
        {
            Text = text;
            Tag = tag;
            Whitespace = whitespace;
            Phonemes = phonemes;
            StartTs = startTs;
            EndTs = endTs;
            _ = underscore ?? new Underscore();
        }

        public record Underscore
        {
            public bool IsHead { get; set; }
            public string Alias { get; set; }
            public double? Stress { get; set; }
            public string Currency { get; set; }
            public string NumFlags { get; set; }
            public bool Prespace { get; set; }
            public int? Rating { get; set; }

            public Underscore()
            {
                NumFlags = "";
            }
        }
    }
    #endregion

    #region Utils
    internal static class Utils
    {
        public static MToken MergeTokens(List<MToken> tokens, string unk = null)
        {
            var stress = tokens.Where(tk => tk._.Stress.HasValue).Select(tk => tk._.Stress.Value).ToHashSet();
            var currency = tokens.Where(tk => tk._.Currency != null).Select(tk => tk._.Currency).ToHashSet();
            var rating = tokens.Select(tk => tk._.Rating).ToHashSet();

            string phonemes = null;
            if (unk != null)
            {
                var phonemeBuilder = new StringBuilder();
                for (int i = 0; i < tokens.Count; i++)
                {
                    var tk = tokens[i];
                    if (i > 0 && tk._.Prespace && phonemeBuilder.Length > 0 &&
                        !char.IsWhiteSpace(phonemeBuilder[phonemeBuilder.Length - 1]) && tk.Phonemes != null)
                    {
                        phonemeBuilder.Append(' ');
                    }
                    phonemeBuilder.Append(tk.Phonemes ?? unk);
                }
                phonemes = phonemeBuilder.ToString();
            }

            var text = string.Join("", tokens.Take(tokens.Count - 1).Select(tk => tk.Text + tk.Whitespace)) + tokens.Last().Text;
            var tag = tokens.OrderByDescending(tk => tk.Text.Sum(c => char.IsLower(c) ? 1 : 2)).First().Tag;

            return new MToken(
                text: text,
                tag: tag,
                whitespace: tokens.Last().Whitespace,
                phonemes: phonemes,
                startTs: tokens.First().StartTs,
                endTs: tokens.Last().EndTs,
                underscore: new MToken.Underscore
                {
                    IsHead = tokens.First()._.IsHead,
                    Alias = null,
                    Stress = stress.Count == 1 ? stress.First() : null,
                    Currency = currency.Any() ? currency.Max() : null,
                    NumFlags = string.Join("", tokens.SelectMany(tk => tk._.NumFlags).OrderBy(c => c).Distinct()),
                    Prespace = tokens.First()._.Prespace,
                    Rating = rating.Contains(null) ? null : rating.Min()
                }
            );
        }

        public static readonly HashSet<char> Diphthongs = new("AIOQWYʤʧ");

        public static int StressWeight(string ps)
        {
            if (string.IsNullOrEmpty(ps))
                return 0;

            return ps.Count(c => Diphthongs.Contains(c)) * 2 + ps.Count(c => !Diphthongs.Contains(c));
        }

        // Stub method for num2words
        public static string Num2Words(int number, string to = "cardinal")
        {
            // In a real implementation, you would convert numbers to words
            // For now, we'll simply return the number as a string with the conversion type
            return $"{number}_{to}";
        }

        public static string Num2Words(float number)
        {
            // Stub for float conversion
            return number.ToString(CultureInfo.InvariantCulture);
        }
    }

    #endregion
    #region Lexicon
    internal class TokenContext
    {
        public bool? FutureVowel { get; set; }
        public bool FutureTo { get; set; }

        public TokenContext(bool? futureVowel = null, bool futureTo = false)
        {
            FutureVowel = futureVowel;
            FutureTo = futureTo;
        }
    }

    internal partial class Lexicon
    {
        // Phonemes and Rating
        internal struct PsRating
        {
            public string Ps { get; set; }
            public int Rating { get; set; }

            public PsRating(string ps, int rating)
            {
                Ps = ps;
                Rating = rating;
            }
        }

        readonly LanguageCode _languageCode;
        internal readonly Dictionary<string, object> _golds;
        internal readonly Dictionary<string, object> _silvers;

        static readonly HashSet<char> GbVocab = new("AIQWYabdfhijklmnpstuvwzðŋɑɒɔəɛɜɡɪɹʃʊʌʒʤʧˈˌːθᵊ");
        static readonly HashSet<char> UsVocab = new("AIOWYbdfhijklmnpstuvwzæðŋɑɔəɛɜɡɪɹɾʃʊʌʒʤʧˈˌθᵊᵻʔ");
        internal HashSet<char> Vocab => _languageCode switch
        {
            LanguageCode.En_GB => GbVocab,
            LanguageCode.En_US => UsVocab,
            _ => throw new NotImplementedException($"Language {_languageCode} is not supported."),
        };

        static readonly HashSet<int> LexiconOrds = new HashSet<int>
        {
            39, 45,
        }.Concat(Enumerable.Range(65, 26)).Concat(Enumerable.Range(97, 26)).ToHashSet();

        const string PrimaryStress = "ˈ";
        const string SecondaryStress = "ˌ";
        const string Stresses = SecondaryStress + PrimaryStress;

        const double CAP_STRESS_LOW = 0.5;
        const double CAP_STRESS_HIGH = 2.0;

        static readonly Dictionary<string, string> AddSymbols = new()
        {
            { ".", "dot" },
            { "/", "slash" }
        };

        static readonly Dictionary<string, string> Symbols = new()
        {
            { "%", "percent" },
            { "&", "and" },
            { "+", "plus" },
            { "@", "at" }
        };

        Lexicon(LanguageCode lang, Dictionary<string, object> gold, Dictionary<string, object> silver)
        {
            _languageCode = lang;
            _golds = gold;
            _silvers = silver;
        }

        static Dictionary<string, object> GrowDictionary(Dictionary<string, object> d)
        {
            // HACK: Inefficient but correct.
            var result = new Dictionary<string, object>(d);
            foreach (var kv in d)
            {
                string k = kv.Key;
                if (k.Length < 2) { continue; }

                string kLower = k.ToLowerInvariant();
                if (k == kLower)
                {
                    string kCapFirst = CapitalizeFirst(k);
                    if (k != kCapFirst)
                    {
                        result[kCapFirst] = kv.Value;
                    }
                }
                else if (k == CapitalizeFirst(kLower))
                {
                    result[kLower] = kv.Value;
                }
            }
            return result;
        }

        static string CapitalizeFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) { return s; }
            return char.ToUpperInvariant(s[0]) + s[1..];
        }

        PsRating GetNNP(string word)
        {
            var ps = word
                .Where(c => char.IsLetter(c))
                .Select(c => _golds.TryGetValue(c.ToString().ToUpperInvariant(), out var val) ? val as string : null);

            if (ps.Contains(null))
                return new PsRating(null, 0);

            var psStr = ApplyStress(string.Join("", ps), 0);
            var parts = psStr.Split(new[] { SecondaryStress }, 2, StringSplitOptions.None);
            return new(string.Join(PrimaryStress, parts), 3);
        }

        PsRating GetSpecialCase(string word, string tag, double? stress, TokenContext ctx)
        {
            if (tag == "ADD" && AddSymbols.TryGetValue(word, out string addSymbol))
            {
                return Lookup(addSymbol, null, -0.5, ctx);
            }
            if (Symbols.TryGetValue(word, out string symbol))
            {
                return Lookup(symbol, null, null, ctx);
            }
            if (word.Contains(".") && word.Trim('.').All(char.IsLetter) && word.Split('.').Max(s => s.Length) < 3)
            {
                return GetNNP(word);
            }
            if (word == "a" || word == "A")
            {
                return new(tag == "DT" ? "ɐ" : "ˈA", 4);
            }
            if (word == "am" || word == "Am" || word == "AM")
            {
                if (tag.StartsWith("NN"))
                    return GetNNP(word);
                else if (ctx.FutureVowel == null || word != "am" || (stress.HasValue && stress > 0))
                    return new(_golds["am"] as string, 4);
                return new("ɐm", 4);
            }
            if (word == "an" || word == "An" || word == "AN")
            {
                if (word == "AN" && tag.StartsWith("NN"))
                    return GetNNP(word);
                return new("ɐn", 4);
            }
            if (word == "I" && tag == "PRP")
            {
                return new($"{SecondaryStress}I", 4);
            }
            if ((word == "by" || word == "By" || word == "BY") && GetParentTag(tag) == "ADV")
            {
                return new("bˈI", 4);
            }
            if (word == "to" || word == "To" || (word == "TO" && (tag == "TO" || tag == "IN")))
            {
                if (ctx.FutureVowel == null)
                    return new(_golds["to"] as string, 4);
                else if (ctx.FutureVowel == false)
                    return new("tə", 4);
                else
                    return new("tʊ", 4);
            }
            if (word == "in" || word == "In" || (word == "IN" && tag != "NNP"))
            {
                string stressStr = (ctx.FutureVowel == null || tag != "IN") ? PrimaryStress : "";
                return new($"{stressStr}ɪn", 4);
            }
            if (word == "the" || word == "The" || (word == "THE" && tag == "DT"))
            {
                return new(ctx.FutureVowel == true ? "ði" : "ðə", 4);
            }
            if (tag == "IN" && Regex.IsMatch(word, @"(?i)vs\.?$"))
            {
                return Lookup("versus", null, null, ctx);
            }
            if (word == "used" || word == "Used" || word == "USED")
            {
                if ((tag == "VBD" || tag == "JJ") && ctx.FutureTo)
                {
                    if (_golds["used"] is Dictionary<string, string> usedDict && usedDict.TryGetValue("VBD", out string vbd))
                        return new(vbd, 4);
                }
                if (_golds["used"] is Dictionary<string, string> usedDefault && usedDefault.TryGetValue("DEFAULT", out string defaultVal))
                    return new(defaultVal, 4);
            }

            return new(null, 0);
        }

        static string GetParentTag(string tag) => tag switch
        {
            null => tag,
            string s when s.StartsWith("VB") => "VERB",
            string s when s.StartsWith("NN") => "NOUN",
            string s when s.StartsWith("ADV") || s.StartsWith("RB") => "ADV", // or tag == 'RP':
            string s when s.StartsWith("ADJ") || s.StartsWith("JJ") => "ADJ",
            _ => tag,
        };

        public bool IsKnown(string word, string tag)
        {
            if (_golds.ContainsKey(word) || Symbols.ContainsKey(word) || _silvers.ContainsKey(word))
                return true;
            else if (!word.All(char.IsLetter) || !word.All(c => LexiconOrds.Contains(c)))
                return false;
            else if (word.Length == 1)
                return true;
            else if (word == word.ToUpperInvariant() && _golds.ContainsKey(word.ToLowerInvariant()))
                return true;

            return word.Substring(1) == word.Substring(1).ToUpperInvariant();
        }

        PsRating Lookup(string word, string tag, double? stress, TokenContext ctx)
        {
            bool? isNNP = null;

            bool isContainsGold = _golds.TryGetValue(word, out object psObj);
            if (word == word.ToUpperInvariant() && !isContainsGold)
            {
                word = word.ToLowerInvariant();
                isNNP = tag == "NNP"; // #Lexicon.get_parent_tag(tag) == 'NOUN'
            }

            int rating = 4;

            if (!isContainsGold && (!isNNP.HasValue || !isNNP.Value))
            {
                _silvers.TryGetValue(word, out psObj);
                rating = 3;
            }

            string ps = null;
            if (psObj is Dictionary<string, string> dict)
            {
                // Handle dictionary lookup
                if (ctx != null && ctx.FutureVowel == null && dict.ContainsKey("None"))
                {
                    tag = "None";
                }
                else if (!dict.ContainsKey(tag))
                {
                    tag = GetParentTag(tag);
                }

                ps = dict.TryGetValue(tag, out var tagValue)
                    ? tagValue
                    : dict["DEFAULT"];
            }

            if (ps == null || (isNNP.HasValue && isNNP.Value && !ps.Contains(PrimaryStress)))
            {
                var nnpResult = GetNNP(word);
                if (nnpResult.Ps != null)
                    return nnpResult;
            }

            return new(ApplyStress(ps, stress), rating);
        }

        static string ApplyStress(string ps, double? stress)
        {
            if (string.IsNullOrEmpty(ps) || !stress.HasValue)
                return ps;

            if (stress < -1)
                return ps.Replace(PrimaryStress, "").Replace(SecondaryStress, "");
            else if (stress == -1 || ((stress == 0 || stress == -0.5) && ps.Contains(PrimaryStress)))
                return ps.Replace(SecondaryStress, "").Replace(PrimaryStress, SecondaryStress);
            else if ((stress == 0 || stress == 0.5 || stress == 1) &&
                    !ps.Contains(PrimaryStress) && !ps.Contains(SecondaryStress))
            {
                if (!ps.Any(c => Constants.Vowels.Contains(c)))
                    return ps;

                return ReStress(SecondaryStress, ps);
            }
            else if (stress >= 1 && !ps.Contains(PrimaryStress) && ps.Contains(SecondaryStress))
                return ps.Replace(SecondaryStress, PrimaryStress);
            else if (stress > 1 && !ps.Contains(PrimaryStress) && !ps.Contains(SecondaryStress))
            {
                if (!ps.Any(c => Constants.Vowels.Contains(c)))
                    return ps;

                return ReStress(PrimaryStress, ps);
            }

            return ps;
        }

        private static string ReStress(string stressMarker, string ps)
        {
            var sb = new StringBuilder();
            bool inserted = false;
            foreach (char c in ps)
            {
                if (c == PrimaryStress[0] || c == SecondaryStress[0])
                    continue; // Remove existing stress markers
                if (!inserted && Constants.Vowels.Contains(c))
                {
                    sb.Append(stressMarker);
                    inserted = true;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static readonly Regex digitRegex = new(@"^[0-9]+$");
        static bool IsDigit(string text) => digitRegex.IsMatch(text);

        // Other methods would be implemented similarly
        private PsRating GetWord(string word, string tag, double? stress, TokenContext ctx)
        {
            // 1. Special cases
            var psRate = GetSpecialCase(word, tag, stress, ctx);
            if (psRate.Ps != null)
            {
                return psRate;
            }
            string wordLower = word.ToLowerInvariant();
            string wordUpper = word.ToUpperInvariant();

            // 2. Case normalization and fallback to lowercase if needed
            if (word.Length > 1
                && word.Replace("'", string.Empty).All(char.IsLetter)
                && word != wordLower
                && (tag != "NNP" || word.Length > 7)
                && !_golds.ContainsKey(word)
                && !_silvers.ContainsKey(word)
                && (word == wordUpper || word.Substring(1) == word.Substring(1).ToLowerInvariant())
                && (_golds.ContainsKey(wordLower)
                    || _silvers.ContainsKey(wordLower)
                    || StemS(wordLower, tag, stress, ctx).Ps != null
                    || StemEd(wordLower, tag, stress, ctx).Ps != null
                    || StemIng(wordLower, tag, stress ?? 0.5, ctx).Ps != null))
            {
                word = wordLower;
            }

            // 3. IsKnown
            if (IsKnown(word, tag))
            {
                return Lookup(word, tag, stress, ctx);
            }
            // 4. Possessive/plural/suffix forms
            else if (word.EndsWith("s'") && IsKnown(word.Substring(0, word.Length - 2) + "'s", tag))
            {
                return Lookup(word.Substring(0, word.Length - 2) + "'s", tag, stress, ctx);
            }
            else if (word.EndsWith("'") && IsKnown(word.Substring(0, word.Length - 1), tag))
            {
                return Lookup(word.Substring(0, word.Length - 1), tag, stress, ctx);
            }
            // 5. Try stemming for -s, -ed, -ing endings
            var sStem = StemS(word, tag, stress, ctx);
            if (sStem.Ps != null)
            {
                return sStem;
            }
            var edStem = StemEd(word, tag, stress, ctx);
            if (edStem.Ps != null)
            {
                return edStem;
            }
            var ingStem = StemIng(word, tag, stress ?? 0.5, ctx);
            if (ingStem.Ps != null)
            {
                return ingStem;
            }

            // 6. Not found
            return new PsRating(null, 0);
        }

        internal PsRating this[MToken tk, TokenContext ctx]
        {
            get
            {
                string word = (tk._.Alias ?? tk.Text)
                    .Replace('\u2018', '\'')
                    .Replace('\u2019', '\'');

                word = word.Normalize(NormalizationForm.FormKC);

                double? stress = null;
                if (word != word.ToLowerInvariant())
                {
                    stress = word == word.ToUpperInvariant()
                        ? CAP_STRESS_HIGH
                        : CAP_STRESS_LOW;
                }

                var result = GetWord(word, tk.Tag, stress, ctx);
                if (result.Ps != null)
                {
                    return new(ApplyStress(AppendCurrency(result.Ps, tk._.Currency), tk._.Stress), result.Rating);
                }
                if (IsNumber(word, tk._.IsHead))
                {
                    var numResult = GetNumber(word, tk._.Currency, tk._.IsHead, tk._.NumFlags);
                    return new(ApplyStress(numResult.Ps, tk._.Stress), numResult.Rating);
                }
                if (!word.All(c => LexiconOrds.Contains(c)))
                {
                    return new(null, 0);
                }
                return new(null, 0);
            }
        }

        // Helper methods

        string AppendCurrency(string ps, string currency)
        {
            if (string.IsNullOrEmpty(currency))
            {
                return ps;
            }
            if (!Constants.Currencies.TryGetValue(currency, out var currencyTuple))
            {
                return ps;
            }
            // else
            currency = StemS($"{currencyTuple.Item1}s", null, null, null).Ps;
            return string.IsNullOrEmpty(currency)
                ? ps
                : $"{ps} {currency}";
        }

        PsRating StemS(string word, string tag, double? stress, TokenContext ctx)
        {
            // Plural/possessive -s/-es/-ies
            if (string.IsNullOrEmpty(word) || word.Length < 3 || !word.EndsWith("s"))
                return new PsRating(null, 0);

            string stem = null;
            if (!word.EndsWith("ss") && IsKnown(word.Substring(0, word.Length - 1), tag))
            {
                stem = word.Substring(0, word.Length - 1);
            }
            else if ((word.EndsWith("'s") || (word.Length > 4 && word.EndsWith("es") && !word.EndsWith("ies"))) && IsKnown(word.Substring(0, word.Length - 2), tag))
            {
                stem = word.Substring(0, word.Length - 2);
            }
            else if (word.Length > 4 && word.EndsWith("ies") && IsKnown(word.Substring(0, word.Length - 3) + "y", tag))
            {
                stem = word.Substring(0, word.Length - 3) + "y";
            }
            else
            {
                return new PsRating(null, 0);
            }

            var stemResult = Lookup(stem, tag, stress, ctx);
            if (stemResult.Ps == null)
                return new PsRating(null, 0);

            // Add correct plural ending
            char last = stemResult.Ps.Length > 0 ? stemResult.Ps[^1] : '\0';
            string plural;
            if ("ptkfθ".Contains(last))
                plural = "s";
            else if ("szʃʒʧʤ".Contains(last))
                plural = _languageCode == LanguageCode.En_GB ? "ɪz" : "ᵻz";
            else
                plural = "z";

            return new PsRating(stemResult.Ps + plural, stemResult.Rating);
        }

        PsRating StemEd(string word, string tag, double? stress, TokenContext ctx)
        {
            // Past tense -ed
            if (string.IsNullOrEmpty(word) || word.Length < 4 || !word.EndsWith("d"))
                return new PsRating(null, 0);

            string stem = null;
            if (!word.EndsWith("dd") && IsKnown(word.Substring(0, word.Length - 1), tag))
            {
                stem = word.Substring(0, word.Length - 1);
            }
            else if (word.Length > 4 && word.EndsWith("ed") && !word.EndsWith("eed") && IsKnown(word.Substring(0, word.Length - 2), tag))
            {
                stem = word.Substring(0, word.Length - 2);
            }
            else
            {
                return new PsRating(null, 0);
            }

            var stemResult = Lookup(stem, tag, stress, ctx);
            if (stemResult.Ps == null)
                return new PsRating(null, 0);

            // Add correct -ed ending
            char last = stemResult.Ps.Length > 0 ? stemResult.Ps[^1] : '\0';
            string ed;
            if ("pkfθʃsʧ".Contains(last))
                ed = "t";
            else if (last == 'd')
                ed = _languageCode == LanguageCode.En_GB ? "ɪd" : "ᵻd";
            else if (last != 't')
                ed = "d";
            else if (_languageCode == LanguageCode.En_GB || stemResult.Ps.Length < 2)
                ed = "ɪd";
            else if (stemResult.Ps.Length > 1 && "AIOWYiuæɑəɛɪɹʊʌ".Contains(stemResult.Ps[^2]))
                ed = "ɾᵻd";
            else
                ed = "ᵻd";

            return new PsRating(stemResult.Ps + ed, stemResult.Rating);
        }

        PsRating StemIng(string word, string tag, double? stress, TokenContext ctx)
        {
            // Present participle -ing
            if (string.IsNullOrEmpty(word) || word.Length < 5 || !word.EndsWith("ing"))
                return new PsRating(null, 0);

            string stem = null;
            if (word.Length > 5 && IsKnown(word.Substring(0, word.Length - 3), tag))
            {
                stem = word.Substring(0, word.Length - 3);
            }
            else if (IsKnown(word.Substring(0, word.Length - 3) + "e", tag))
            {
                stem = word.Substring(0, word.Length - 3) + "e";
            }
            else if (word.Length > 5 && System.Text.RegularExpressions.Regex.IsMatch(word, @"([bcdgklmnprstvxz])\1ing$|cking$") && IsKnown(word.Substring(0, word.Length - 4), tag))
            {
                stem = word.Substring(0, word.Length - 4);
            }
            else
            {
                return new PsRating(null, 0);
            }

            var stemResult = Lookup(stem, tag, stress, ctx);
            if (stemResult.Ps == null)
                return new PsRating(null, 0);

            // Add correct -ing ending
            string ing;
            if (_languageCode == LanguageCode.En_GB)
            {
                // TODO: British-specific logic for 'əː' endings if needed
                ing = "ɪŋ";
            }
            else if (stemResult.Ps.Length > 1 && stemResult.Ps[^1] == 't' && "AIOWYiuæɑəɛɪɹʊʌ".Contains(stemResult.Ps[^2]))
            {
                ing = "ɾɪŋ";
                stemResult = new PsRating(stemResult.Ps.Substring(0, stemResult.Ps.Length - 1), stemResult.Rating);
            }
            else
            {
                ing = "ɪŋ";
            }

            return new PsRating(stemResult.Ps + ing, stemResult.Rating);
        }

        static bool IsNumber(string word, bool isHead)
        {
            if (word.All(c => !IsDigit(c.ToString())))
                return false;

            var suffixes = new List<string> { "ing", "'d", "ed", "'s" }.Concat(Constants.Ordinals).Append("s");
            foreach (var s in suffixes)
            {
                if (word.EndsWith(s))
                {
                    word = word.Substring(0, word.Length - s.Length);
                    break;
                }
            }

            return word.Select((c, i) =>
                IsDigit(c.ToString()) || c == ',' || c == '.' ||
                (isHead && i == 0 && c == '-')).All(x => x);
        }

        public PsRating GetNumber(string word, string currency, bool isHead, string numFlags)
        {
            // Implementation for processing numbers
            return new(null, 0);
        }
    }
    #endregion

    #region G2P
    public class MisakiEnglishG2P : IG2P
    {
        LanguageCode _languageCode;
        Lexicon _lexicon;
        readonly string _unk;
        Pipeline _nlp;

        public MisakiEnglishG2P(bool trf = false, string unk = "❓")
        {
            Catalyst.Models.English.Register();
            _unk = unk;
        }

        public void Dispose()
        {
        }

        public async Task InitializeAsync(LanguageCode langCode, CancellationToken cancellationToken)
        {
            _languageCode = langCode;

            _lexicon = await Lexicon.CreateAsync(langCode, cancellationToken);

            // Initialize Catalyst model
            var language = langCode switch
            {
                LanguageCode.En_GB => Language.English,
                LanguageCode.En_US => Language.English,
                _ => throw new NotImplementedException(),
            };
            _nlp = await Pipeline.ForAsync(language);
        }

        public string Convert(ReadOnlySpan<char> input)
        {
            var result = Convert(input.ToString());
            return result.Item1;
        }

        static Tuple<string, List<string>, Dictionary<int, object>> Preprocess(string text)
        {
            var result = new StringBuilder();
            var tokens = new List<string>();
            var features = new Dictionary<int, object>();

            int lastEnd = 0;
            text = text.TrimStart();

            foreach (Match m in Constants.LinkRegex.Matches(text))
            {
                result.Append(text[lastEnd..m.Index]);
                tokens.AddRange(text[lastEnd..m.Index].Split());

                var f = m.Groups[2].Value;
                object feature = null;

                if (IsDigit(f.TrimStart('-', '+')))
                    feature = int.Parse(f);
                else if (f == "0.5" || f == "+0.5")
                    feature = 0.5;
                else if (f == "-0.5")
                    feature = -0.5;
                else if (f.Length > 1 && f[0] == '/' && f[f.Length - 1] == '/')
                    feature = "/" + f[1..^1];
                else if (f.Length > 1 && f[0] == '#' && f[f.Length - 1] == '#')
                    feature = "#" + f[1..^1];

                if (feature != null)
                    features[tokens.Count] = feature;

                result.Append(m.Groups[1].Value);
                tokens.AddRange(m.Groups[1].Value.Split());
                lastEnd = m.Index + m.Length;
            }

            if (lastEnd < text.Length)
            {
                result.Append(text[lastEnd..]);
                tokens.AddRange(text[lastEnd..].Split());
            }

            return Tuple.Create(result.ToString(), tokens, features);
        }

        static bool IsDigit(string s)
        {
            return !string.IsNullOrEmpty(s) && s.All(char.IsDigit);
        }

        List<MToken> Tokenize(string text, List<string> tokens, Dictionary<int, object> features)
        {
            // Use Catalyst to tokenize and tag the text
            var doc = new Document(text, Language.English);

            IDocument document = _nlp.ProcessSingle(doc);

            var mutableTokens = document.ToTokenList().Select(token => new MToken(
                text: token.OriginalValue,
                tag: token.POS.ToString(),
                whitespace: (token.PreviousChar.HasValue && char.IsWhiteSpace(token.PreviousChar.Value))
                    ? " "
                    : "",
                underscore: new MToken.Underscore { IsHead = true, NumFlags = "", Prespace = false }
            )).ToList();

            if (features.Count == 0)
            {
                return mutableTokens;
            }

            // Implement alignment between original tokens and processed tokens
            // This is a simplified version
            var alignment = AlignTokens(tokens, mutableTokens.Select(t => t.Text).ToList());

            foreach (var kv in features)
            {
                var k = kv.Key;
                var v = kv.Value;

                var indices = alignment.Where(a => a.Item1 == k).Select(a => a.Item2).ToList();

                for (int i = 0; i < indices.Count; i++)
                {
                    var j = indices[i];
                    if (j >= mutableTokens.Count)
                        continue;

                    if (v is double dbl)
                        mutableTokens[j]._.Stress = dbl;
                    else if (v is string str)
                    {
                        if (str.StartsWith("/"))
                        {
                            mutableTokens[j]._.IsHead = i == 0;
                            mutableTokens[j].Phonemes = i == 0 ? str.TrimStart('/') : "";
                            mutableTokens[j]._.Rating = 5;
                        }
                        else if (str.StartsWith("#"))
                        {
                            mutableTokens[j]._.NumFlags = str.TrimStart('#');
                        }
                    }
                }
            }

            return mutableTokens;
        }

        // Simple token alignment algorithm
        private List<Tuple<int, int>> AlignTokens(List<string> sourceTokens, List<string> destTokens)
        {
            var result = new List<Tuple<int, int>>();
            int j = 0;

            for (int i = 0; i < sourceTokens.Count; i++)
            {
                if (j < destTokens.Count)
                {
                    result.Add(Tuple.Create(i, j));
                    j++;
                }
            }

            return result;
        }

        List<MToken> FoldLeft(List<MToken> tokens)
        {
            var result = new List<MToken>();

            foreach (var tk in tokens)
            {
                if (result.Count > 0 && !tk._.IsHead)
                    result.Add(Utils.MergeTokens(new List<MToken> { result[result.Count - 1], tk }, _unk));
                else
                    result.Add(tk);
            }

            return result;
        }

        static List<object> Retokenize(List<MToken> tokens)
        {
            // TODO: Implementation of retokenize would go here
            var words = new List<object>(tokens);
            string currency = null;
            return words;
        }

        static TokenContext UpdateTokenContext(TokenContext ctx, string ps, MToken token)
        {
            bool? vowel = ctx.FutureVowel;

            // Update vowel context based on phoneme string if available
            if (!string.IsNullOrEmpty(ps))
            {
                // Find the first character that's either a vowel, consonant or punctuation
                foreach (char c in ps)
                {
                    if (Constants.NonQuotePuncts.Contains(c))
                    {
                        vowel = null;
                        break;
                    }
                    else if (Constants.Vowels.Contains(c) || Constants.Consonants.Contains(c))
                    {
                        vowel = Constants.Vowels.Contains(c);
                        break;
                    }
                }
            }

            // Check if the token is a form of "to"
            bool futureTo = token.Text == "to" || token.Text == "To" ||
                           (token.Text == "TO" && (token.Tag == "TO" || token.Tag == "IN"));

            return new TokenContext(futureVowel: vowel, futureTo: futureTo);
        }

        static void ResolveTokens(List<MToken> tokens)
        {
            // Implementation would go here
        }

        Tuple<string, List<MToken>> Convert(string text)
        {
            var preprocessResult = Preprocess(text);

            var tokens = Tokenize(
                preprocessResult.Item1,
                preprocessResult.Item2,
                preprocessResult.Item3
            );

            tokens = FoldLeft(tokens);

            // var retokenized = Retokenize(tokens);

            var ctx = new TokenContext();

            // // Process tokens in reverse order
            // for (int i = retokenized.Count - 1; i >= 0; i--)
            // {
            //     var w = retokenized[i];
            //     // Process each token
            //     // Implementation would go here
            // }

            // Final processing and result generation
            // Implementation would go here

            // Composite result text
            var sb = new StringBuilder();
            foreach (var token in tokens)
            {
                if (string.IsNullOrEmpty(token.Phonemes))
                {
                    sb.Append(_unk);
                }
                else
                {
                    sb.Append(token.Text);
                }
                sb.Append(token.Whitespace);
            }

            return Tuple.Create(sb.ToString(), tokens);
        }
    }
    #endregion
}
