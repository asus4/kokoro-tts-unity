using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
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

        internal LanguageCode languageCode;
        internal FrozenDictionary<string, FrozenDictionary<Tag, string>> _golds;
        internal FrozenDictionary<string, string> _silvers;

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

            return string.Empty;
        }

        internal bool TryGet(string word, Tag tag, out string phoneme)
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
    }
}
