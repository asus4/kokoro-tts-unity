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
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Kokoro.Misaki
{
    #region MToken
    internal class MToken
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

        public class Underscore
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
                    phonemeBuilder.Append(tk.Phonemes == null ? unk : tk.Phonemes);
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

    internal class Lexicon
    {
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

        static readonly Tuple<double, double> _capStresses = Tuple.Create(0.5, 2.0);
        static readonly HashSet<char> UsTaus = new("AIOWYiuæɑəɛɪɹʊʌ");
        static readonly HashSet<char> Vowels = new("AIOQWYaiuæɑɒɔəɛɜɪʊʌᵻ");
        static readonly HashSet<char> Consonants = new("bdfhjklmnpstvwzðŋɡɹɾʃʒʤʧθ");
        static readonly HashSet<string> Ordinals = new() { "st", "nd", "rd", "th" };
        static readonly Dictionary<string, Tuple<string, string>> Currencies = new()
        {
            { "$", Tuple.Create("dollar", "cent") },
            { "£", Tuple.Create("pound", "pence") },
            { "€", Tuple.Create("euro", "cent") }
        };

        static readonly HashSet<int> LexiconOrds = new HashSet<int>
        {
            39, 45,
        }.Concat(Enumerable.Range(65, 26)).Concat(Enumerable.Range(97, 26)).ToHashSet();

        const string PrimaryStress = "ˈ";
        const string SecondaryStress = "ˌ";
        const string Stresses = SecondaryStress + PrimaryStress;

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

        public Lexicon(LanguageCode lang, Dictionary<string, object> gold, Dictionary<string, object> silver)
        {
            _languageCode = lang;
            _golds = gold;
            _silvers = silver;
        }

        public static async Awaitable<Lexicon> CreateAsync(LanguageCode lang, CancellationToken cancellationToken)
        {
            await Awaitable.MainThreadAsync();
            const string prefix = "KokoroTTS/Misaki";

            bool isBritish = lang switch
            {
                LanguageCode.En_GB => true,
                LanguageCode.En_US => false,
                _ => throw new NotImplementedException($"Language {lang} is not supported.")
            };
            string goldPath = isBritish ? $"{prefix}/gb_gold" : $"{prefix}/us_gold";
            string silverPath = isBritish ? $"{prefix}/gb_silver" : $"{prefix}/us_silver";
            var gold = await LoadDictResource(goldPath, cancellationToken);
            var silver = await LoadDictResource(silverPath, cancellationToken);
            gold = GrowDictionary(gold);
            silver = GrowDictionary(silver);
            var lexicon = new Lexicon(lang, gold, silver);

            await Awaitable.MainThreadAsync();
            return lexicon;
        }

        static async Awaitable<Dictionary<string, object>> LoadDictResource(string path, CancellationToken cancellationToken)
        {
            var request = Resources.LoadAsync<TextAsset>(path);
            await request;
            var asset = request.asset as TextAsset;
            string text = asset.text;

            var rawDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
            // JObject -> Dictionary<string, string>
            var dict = new Dictionary<string, object>(rawDict.Count);
            foreach (var kv in rawDict)
            {
                if (kv.Value is string str)
                {
                    dict[kv.Key] = str;
                }
                else if (kv.Value is JObject jObj)
                {
                    // JObject -> Dictionary<string, string>
                    dict[kv.Key] = jObj.ToObject<Dictionary<string, string>>();
                }
            }
            await Awaitable.BackgroundThreadAsync();
            return dict;
        }

        static Dictionary<string, object> GrowDictionary(Dictionary<string, object> d)
        {
            // HACK: Inefficient but correct.
            var result = new Dictionary<string, object>(d);
            foreach (var kv in d)
            {
                string k = kv.Key;
                if (k.Length < 2) { continue; }

                string kLower = k.ToLower();
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
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        public Tuple<string, int> GetNNP(string word)
        {
            var ps = word
                .Where(c => char.IsLetter(c))
                .Select(c => _golds.TryGetValue(c.ToString().ToUpper(), out var val) ? val as string : null)
                .ToList();

            if (ps.Contains(null))
                return Tuple.Create<string, int>(null, 0);

            var psStr = ApplyStress(string.Join("", ps), 0);
            var parts = psStr.Split(new[] { SecondaryStress }, 2, StringSplitOptions.None);
            return Tuple.Create(string.Join(PrimaryStress, parts), 3);
        }

        public Tuple<string, int> GetSpecialCase(string word, string tag, double? stress, TokenContext ctx)
        {
            if (tag == "ADD" && AddSymbols.ContainsKey(word))
                return Lookup(AddSymbols[word], null, -0.5, ctx);
            else if (Symbols.ContainsKey(word))
                return Lookup(Symbols[word], null, null, ctx);
            else if (word.Contains(".") && word.Trim('.').All(char.IsLetter) &&
                    word.Split('.').Max(s => s.Length) < 3)
                return GetNNP(word);
            else if (word == "a" || word == "A")
                return Tuple.Create(tag == "DT" ? "ɐ" : "ˈA", 4);
            else if (word == "am" || word == "Am" || word == "AM")
            {
                if (tag.StartsWith("NN"))
                    return GetNNP(word);
                else if (ctx.FutureVowel == null || word != "am" || (stress.HasValue && stress > 0))
                    return Tuple.Create(_golds["am"] as string, 4);
                return Tuple.Create("ɐm", 4);
            }
            // Implement other special cases as needed

            return Tuple.Create<string, int>(null, 0);
        }

        public static string GetParentTag(string tag)
        {
            if (tag == null)
                return tag;
            else if (tag.StartsWith("VB"))
                return "VERB";
            else if (tag.StartsWith("NN"))
                return "NOUN";
            else if (tag.StartsWith("ADV") || tag.StartsWith("RB"))
                return "ADV";
            else if (tag.StartsWith("ADJ") || tag.StartsWith("JJ"))
                return "ADJ";
            return tag;
        }

        public bool IsKnown(string word, string tag)
        {
            if (_golds.ContainsKey(word) || Symbols.ContainsKey(word) || _silvers.ContainsKey(word))
                return true;
            else if (!word.All(char.IsLetter) || !word.All(c => LexiconOrds.Contains(c)))
                return false;
            else if (word.Length == 1)
                return true;
            else if (word == word.ToUpper() && _golds.ContainsKey(word.ToLower()))
                return true;

            return word.Substring(1) == word.Substring(1).ToUpper();
        }

        public Tuple<string, int> Lookup(string word, string tag, double? stress, TokenContext ctx)
        {
            bool? isNNP = null;
            if (word == word.ToUpper() && !_golds.ContainsKey(word))
            {
                word = word.ToLower();
                isNNP = tag == "NNP";
            }

            object psObj = null;
            int rating = 4;

            if (_golds.TryGetValue(word, out psObj))
            {
                // Found in golds
            }
            else if (!isNNP.HasValue || !isNNP.Value)
            {
                if (_silvers.TryGetValue(word, out psObj))
                    rating = 3;
            }

            string ps = null;
            if (psObj is string strPs)
            {
                ps = strPs;
            }
            else if (psObj is Dictionary<string, string> dict)
            {
                // Handle dictionary lookup
                if (ctx != null && ctx.FutureVowel == null && dict.ContainsKey("None"))
                    tag = "None";
                else if (!dict.ContainsKey(tag))
                    tag = GetParentTag(tag);

                ps = dict.TryGetValue(tag, out var tagValue) ? tagValue : dict["DEFAULT"];
            }

            if (ps == null || (isNNP.HasValue && isNNP.Value && !ps.Contains(PrimaryStress)))
            {
                var nnpResult = GetNNP(word);
                if (nnpResult.Item1 != null)
                    return nnpResult;
            }

            return Tuple.Create(ApplyStress(ps, stress), rating);
        }

        public static string ApplyStress(string ps, double? stress)
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
                if (!ps.Any(c => Vowels.Contains(c)))
                    return ps;

                // Implementation of restress would go here
                return SecondaryStress + ps;
            }
            else if (stress >= 1 && !ps.Contains(PrimaryStress) && ps.Contains(SecondaryStress))
                return ps.Replace(SecondaryStress, PrimaryStress);
            else if (stress > 1 && !ps.Contains(PrimaryStress) && !ps.Contains(SecondaryStress))
            {
                if (!ps.Any(c => Vowels.Contains(c)))
                    return ps;

                // Implementation of restress would go here
                return PrimaryStress + ps;
            }

            return ps;
        }

        static readonly Regex digitRegex = new(@"^[0-9]+$");
        static bool IsDigit(string text) => digitRegex.IsMatch(text);

        // Other methods would be implemented similarly
        public Tuple<string, int> GetWord(string word, string tag, double? stress, TokenContext ctx)
        {
            var specialCase = GetSpecialCase(word, tag, stress, ctx);
            if (specialCase.Item1 != null)
                return specialCase;

            // The rest of the implementation would go here

            return Tuple.Create<string, int>(null, 0);
        }

        public Tuple<string, int> this[MToken tk, TokenContext ctx]
        {
            get
            {
                string word = (tk._.Alias == null ? tk.Text : tk._.Alias)
                    .Replace('\u2018', '\'')
                    .Replace('\u2019', '\'');

                word = NormalizeString(word);

                double? stress = null;
                if (word != word.ToLower())
                    stress = word == word.ToUpper() ? _capStresses.Item2 : _capStresses.Item1;

                var result = GetWord(word, tk.Tag, stress, ctx);
                if (result.Item1 != null)
                    return Tuple.Create(ApplyStress(AppendCurrency(result.Item1, tk._.Currency), tk._.Stress), result.Item2);
                else if (IsNumber(word, tk._.IsHead))
                {
                    var numResult = GetNumber(word, tk._.Currency, tk._.IsHead, tk._.NumFlags);
                    return Tuple.Create(ApplyStress(numResult.Item1, tk._.Stress), numResult.Item2);
                }
                else if (!word.All(c => LexiconOrds.Contains(c)))
                    return Tuple.Create<string, int>(null, 0);

                return Tuple.Create<string, int>(null, 0);
            }
        }

        // Helper methods
        private string NormalizeString(string s)
        {
            // Implementation for normalizing strings would go here
            return s;
        }

        private string AppendCurrency(string ps, string currency)
        {
            if (string.IsNullOrEmpty(currency))
                return ps;

            if (Currencies.TryGetValue(currency, out var currencyTuple))
            {
                var currencyStr = StemS(currencyTuple.Item1 + "s", null, null, null).Item1;
                return currencyStr != null ? $"{ps} {currencyStr}" : ps;
            }

            return ps;
        }

        public Tuple<string, int> StemS(string word, string tag, double? stress, TokenContext ctx)
        {
            // Implementation for stemming -s endings
            return Tuple.Create<string, int>(null, 0);
        }

        private static bool IsNumber(string word, bool isHead)
        {
            if (word.All(c => !IsDigit(c.ToString())))
                return false;

            var suffixes = new List<string> { "ing", "'d", "ed", "'s" }.Concat(Ordinals).Append("s");
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

        public Tuple<string, int> GetNumber(string word, string currency, bool isHead, string numFlags)
        {
            // Implementation for processing numbers
            return Tuple.Create<string, int>(null, 0);
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

        static readonly Regex LinkRegex = new(@"\[([^\]]+)\]\(([^\)]*)\)");

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
            var result = Convert(input.ToString(), preprocess: true);
            return result.Item1;
        }

        public static Tuple<string, List<string>, Dictionary<int, object>> Preprocess(string text)
        {
            var result = new StringBuilder();
            var tokens = new List<string>();
            var features = new Dictionary<int, object>();

            int lastEnd = 0;
            text = text.TrimStart();

            foreach (Match m in LinkRegex.Matches(text))
            {
                result.Append(text.Substring(lastEnd, m.Index - lastEnd));
                tokens.AddRange(text.Substring(lastEnd, m.Index - lastEnd).Split());

                var f = m.Groups[2].Value;
                object feature = null;

                if (IsDigit(f.TrimStart('-', '+')))
                    feature = int.Parse(f);
                else if (f == "0.5" || f == "+0.5")
                    feature = 0.5;
                else if (f == "-0.5")
                    feature = -0.5;
                else if (f.Length > 1 && f[0] == '/' && f[f.Length - 1] == '/')
                    feature = "/" + f.Substring(1, f.Length - 2);
                else if (f.Length > 1 && f[0] == '#' && f[f.Length - 1] == '#')
                    feature = "#" + f.Substring(1, f.Length - 2);

                if (feature != null)
                    features[tokens.Count] = feature;

                result.Append(m.Groups[1].Value);
                tokens.Add(m.Groups[1].Value);
                lastEnd = m.Index + m.Length;
            }

            if (lastEnd < text.Length)
            {
                result.Append(text.Substring(lastEnd));
                tokens.AddRange(text.Substring(lastEnd).Split());
            }

            return Tuple.Create(result.ToString(), tokens, features);
        }

        private static bool IsDigit(string s)
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
                return mutableTokens;

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
            var words = new List<object>();
            string currency = null;

            // Implementation of retokenize would go here

            return words;
        }

        static TokenContext TokenContext(TokenContext ctx, string ps, MToken token)
        {
            bool? vowel = ctx.FutureVowel;
            // Implementation would go here
            bool futureTo = token.Text == "to" || token.Text == "To" ||
                           (token.Text == "TO" && (token.Tag == "TO" || token.Tag == "IN"));

            return new TokenContext(futureVowel: vowel, futureTo: futureTo);
        }

        static void ResolveTokens(List<MToken> tokens)
        {
            // Implementation would go here
        }

        Tuple<string, List<MToken>> Convert(string text, bool preprocess = true)
        {
            var preprocessResult = preprocess
                ? Preprocess(text)
                : Tuple.Create(text, new List<string>(), new Dictionary<int, object>());

            var tokens = Tokenize(
                preprocessResult.Item1,
                preprocessResult.Item2,
                preprocessResult.Item3
            );

            tokens = FoldLeft(tokens);
            var retokenized = Retokenize(tokens);

            var ctx = new TokenContext();

            // Process tokens in reverse order
            for (int i = retokenized.Count - 1; i >= 0; i--)
            {
                var w = retokenized[i];
                // Process each token
                // Implementation would go here
            }

            // Final processing and result generation
            // Implementation would go here

            return Tuple.Create("", new List<MToken>());
        }
    }
    #endregion
}
