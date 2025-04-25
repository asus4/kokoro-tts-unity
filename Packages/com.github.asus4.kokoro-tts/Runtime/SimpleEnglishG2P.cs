using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using Catalyst;
using UnityEngine;

namespace Kokoro
{
    /// <summary>
    /// Simple G2P implementation for English.
    /// </summary>
    public sealed class SimpleEnglishG2P : IG2P
    {
        /// <summary>
        /// Part-of-Speech Tagging
        /// https://universaldependencies.org/u/pos/
        /// </summary>
        internal enum Tag
        {
            None,
            DEFAULT,
            NOUN,
            VERB,
            ADJ,
            ADV,
            VBD,
            VBN,
            VBP,
            DT,
        }

        static readonly HashSet<char> US_TAUS = new("AIOWYiuæɑəɛɪɹʊʌ");
        static readonly Dictionary<string, string> PUNCT_TAG_PHONEMES = new()
        {
            {"‘", "“"},
            {"’", "”"},
            {"«", "“"},
            {"»", "”"},
        };

        LanguageCode languageCode;
        FrozenDictionary<string, FrozenDictionary<Tag, string>> _golds;
        FrozenDictionary<string, string> _silvers;
        Pipeline _nlp;
        const char UNKNOWN = '❓';

        public bool Verbose { get; set; } = false;

        public SimpleEnglishG2P()
        {
            Catalyst.Models.English.Register();
        }

        public void Dispose()
        {
        }

        public async Task InitializeAsync(LanguageCode lang, CancellationToken cancellationToken)
        {
            languageCode = lang;

            bool isBritish = lang switch
            {
                LanguageCode.En_GB => true,
                LanguageCode.En_US => false,
                _ => throw new NotImplementedException($"Language {lang} is not supported.")
            };

            const string prefix = "KokoroTTS/Misaki";
            string goldPath = isBritish ? $"{prefix}/gb_gold" : $"{prefix}/us_gold";
            string silverPath = isBritish ? $"{prefix}/gb_silver" : $"{prefix}/us_silver";

            var goldsRaw = await LoadJsonFromResources<Dictionary<string, JsonElement>>(goldPath);
            var silver = await LoadJsonFromResources<Dictionary<string, string>>(silverPath);
            await Awaitable.BackgroundThreadAsync();

            // TODO: Consider using GetAlternateLookup for performance, (which is not supported in Unity yet)
            _golds = goldsRaw.Select(FlattenGold).ToFrozenDictionary();
            _silvers = silver.ToFrozenDictionary();

            _nlp = await Pipeline.ForAsync(Mosaik.Core.Language.English);
        }

        /// <summary>
        /// Post-process for gold.json:
        /// Flatten (string || Dictionary<string, string>) to FrozenDictionary<Tag, string>
        /// </summary>
        /// <param name="kv"></param>
        /// <returns></returns>
        static KeyValuePair<string, FrozenDictionary<Tag, string>> FlattenGold(KeyValuePair<string, JsonElement> kv)
        {
            var value = kv.Value;

            if (value.ValueKind == JsonValueKind.String)
            {
                var dict = new Dictionary<Tag, string> { { Tag.DEFAULT, value.GetString() } };
                return new(kv.Key, dict.ToFrozenDictionary());
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                var dict = value.EnumerateObject().Select(obj =>
                {
                    if (Enum.TryParse(obj.Name, false, out Tag tag))
                    {
                        return new KeyValuePair<Tag, string>(tag, obj.Value.GetString() ?? string.Empty);
                    }
                    else
                    {
                        throw new NotSupportedException($"Unsupported Tag: key:{kv.Key}, tag:{obj.Name}");
                    }
                }).ToFrozenDictionary();
                return new(kv.Key, dict);
            }

            throw new NotSupportedException($"Unsupported JsonElement type: {value.ValueKind}, {value.ToString()}");
        }

        static async Task<T> LoadJsonFromResources<T>(string path)
        {
            await Awaitable.MainThreadAsync();
            var request = Resources.LoadAsync<TextAsset>(path);

            try
            {
                await request;
                var asset = request.asset as TextAsset;
                var utf8Json = asset.bytes;
                await Awaitable.BackgroundThreadAsync();
                return JsonSerializer.Deserialize<T>(utf8Json);
            }
            finally
            {
                await Awaitable.MainThreadAsync();
                Resources.UnloadAsset(request.asset);
            }
        }

        public string Convert(ReadOnlySpan<char> text)
        {
            if (text.IsEmpty)
            {
                return string.Empty;
            }
            var doc = new Document(text.ToString(), Mosaik.Core.Language.English);

            IDocument document = _nlp.ProcessSingle(doc);
            var tokens = document.ToTokenList();

            if (Verbose)
            {
                UnityEngine.Debug.Log(document.ToJson());
            }

            var phonemes = new StringBuilder(text.Length);
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (TryGet(token, out string phoneme))
                {
                    phonemes.Append(phoneme);
                }
                else
                {
                    if (Verbose)
                    {
                        UnityEngine.Debug.Log(token.ToDebugString());
                    }
                    phonemes.Append(UNKNOWN);
                }

                // Fill characters between tokens
                if (i < tokens.Count - 1)
                {
                    var nextToken = tokens[i + 1];
                    var spaces = text[(token.End + 1)..nextToken.Begin].ToString()
                        .Select(c => c switch
                        {
                            ' ' or '\n' or '\t' => ' ',
                            _ => UNKNOWN
                        })
                        .Distinct();
                    foreach (char c in spaces)
                    {
                        phonemes.Append(c);
                    }
                }
            }
            return phonemes.ToString();
        }


        bool TryGet(IToken token, out string phoneme)
        {
            string word = token.Value;
            if (string.IsNullOrEmpty(word))
            {
                phoneme = string.Empty;
                return true;
            }

            PartOfSpeech pos = token.POS;

            // Get phoneme for Punctuation
            if (pos == PartOfSpeech.PUNCT)
            {
                if (PUNCT_TAG_PHONEMES.TryGetValue(word, out string phonemeValue))
                {
                    phoneme = phonemeValue;
                    return true;
                }
                phoneme = token.Value;
                return true;
            }

            // Default lookup
            Tag tag = PosToTag(pos);
            if (TryGet(word, tag, out phoneme))
            {
                return true;
            }

            // Lookup with capitalization, lowercase, and uppercase
            if (TryGet(word.CapitalizeFirstLetter(), tag, out phoneme))
            {
                return true;
            }
            if (TryGet(word.ToLowerInvariant(), tag, out phoneme))
            {
                return true;
            }
            if (TryGet(word.ToUpperInvariant(), tag, out phoneme))
            {
                return true;
            }

            // Stemmed lookup (s, ed, ing)
            if (TryGetStemmed(token, tag, out phoneme))
            {
                return true;
            }

            // TODO: Implement transformer based fallback here
            // https://github.com/hexgrad/misaki/pull/74

            phoneme = string.Empty;
            return false;
        }

        /// <summary>
        /// Simply get phoneme from gold or silver Lexicon. No fallback.
        /// </summary>
        /// <param name="word">A word</param>
        /// <param name="tag">POS Tag</param>
        /// <param name="phoneme">Result phoneme</param>
        /// <returns>True if phoneme is found, otherwise false.</returns>
        bool TryGet(string word, Tag tag, out string phoneme)
        {
            if (_golds.TryGetValue(word, out var gold))
            {
                if (gold.TryGetValue(tag, out phoneme)
                    // Might contain null in gold.json
                    && !string.IsNullOrEmpty(phoneme))
                {
                    return true;
                }
                if (gold.TryGetValue(Tag.DEFAULT, out phoneme))
                {
                    return true;
                }
            }
            return _silvers.TryGetValue(word, out phoneme);
        }


        bool TryGetStemmed(IToken token, Tag tag, out string phoneme)
        {
            if (!token.TryGetLemma(out string lemma, out string stem))
            {
                phoneme = string.Empty;
                return false;
            }
            if (!TryGet(lemma, tag, out string lemmaPhoneme))
            {
                phoneme = string.Empty;
                return false;
            }

            bool isBritish = languageCode == LanguageCode.En_GB;

            switch (stem)
            {
                // https://en.wiktionary.org/wiki/-s
                case "s":
                    phoneme = lemmaPhoneme + lemmaPhoneme[^1] switch
                    {
                        char c when "ptkfθ".Contains(c) => "z",
                        char c when "szʃʒʧʤ".Contains(c) => isBritish ? "ɪs" : "ᵻz",
                        _ => "z",
                    };
                    return true;
                // https://en.wiktionary.org/wiki/-ed
                case "ed":
                    phoneme = lemmaPhoneme[^1] switch
                    {
                        char c when "pkfθʃsʧ".Contains(c) => lemmaPhoneme + "t",
                        'd' => lemmaPhoneme + (isBritish ? "ɪd" : "ᵻd"),
                        char c when c != 't' => lemmaPhoneme + "d",
                        char _ when isBritish || lemmaPhoneme.Length < 2 => lemmaPhoneme + "ɪd",
                        char _ when US_TAUS.Contains(lemmaPhoneme[^2]) => lemmaPhoneme[..-1] + "ɾᵻd",
                        _ => lemmaPhoneme + "ᵻd",
                    };
                    return true;
                // ing
                case "ing":
                    phoneme = lemmaPhoneme + lemmaPhoneme[^1] switch
                    {
                        char c when "ptkfθʃszʧʤ".Contains(c) => "ɪŋ",
                        char c when "bdgʒ".Contains(c) => "ɪn",
                        char c when US_TAUS.Contains(c) => "ᵻŋ",
                        _ => lemmaPhoneme + "ɪŋ",
                    };
                    return true;
                default:
                    if (Verbose)
                    {
                        UnityEngine.Debug.Log($"Unhandled stem: {stem} for lemma: {lemma}, phoneme: {lemmaPhoneme}");
                    }
                    phoneme = string.Empty;
                    return false;
            }
        }


        static Tag PosToTag(PartOfSpeech pos) => pos switch
        {
            PartOfSpeech.NONE => Tag.None,
            PartOfSpeech.ADJ => Tag.ADJ,
            PartOfSpeech.ADV => Tag.ADV,
            PartOfSpeech.NOUN => Tag.NOUN,
            PartOfSpeech.VERB => Tag.VERB,
            PartOfSpeech.DET => Tag.DT,
            _ => Tag.DEFAULT,
        };


    }
}
