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

            _golds = goldsRaw.Select(FlattenGold).ToFrozenDictionary();
            _silvers = silver.ToFrozenDictionary();
            // Debug.Log($"G2P: {goldsRaw.Count} golds, {silver.Count} silvers");

            _nlp = await Pipeline.ForAsync(Mosaik.Core.Language.English);
        }

        /// <summary>
        /// Post-process For gold.json:
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

        static readonly Dictionary<string, string> PUNCT_TAG_PHONEMES = new()
        {
            {"‘", "“"},
            {"’", "”"},
            {"«", "“"},
            {"»", "”"},
        };

        bool TryGet(IToken token, out string phoneme)
        {
            string word = token.Value;
            if (string.IsNullOrEmpty(word))
            {
                phoneme = string.Empty;
                return true;
            }

            PartOfSpeech pos = token.POS;

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

            Tag tag = PosToTag(pos);

            if (TryGet(word, tag, out phoneme))
            {
                return true;
            }

            // Check with capitalization, lowercase, and uppercase
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

            // TODO: fallback

            phoneme = string.Empty;
            return false;
        }

        bool TryGet(string word, Tag tag, out string phoneme)
        {
            if (_golds.TryGetValue(word, out var gold))
            {
                if (gold.TryGetValue(tag, out string goldPhoneme))
                {
                    phoneme = goldPhoneme;
                    return true;
                }
                if (gold.TryGetValue(Tag.DEFAULT, out goldPhoneme))
                {
                    phoneme = goldPhoneme;
                    return true;
                }
            }
            if (_silvers.TryGetValue(word, out string silver))
            {
                phoneme = silver.ToString();
                return true;
            }
            phoneme = string.Empty;
            return false;
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
