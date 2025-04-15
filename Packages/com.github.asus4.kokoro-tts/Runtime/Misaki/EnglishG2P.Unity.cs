using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Kokoro.Misaki
{
    internal partial class Lexicon
    {
        public static async Task<Lexicon> CreateAsync(LanguageCode lang, CancellationToken cancellationToken)
        {
            await Awaitable.MainThreadAsync();
            bool isBritish = lang switch
            {
                LanguageCode.En_GB => true,
                LanguageCode.En_US => false,
                _ => throw new NotImplementedException($"Language {lang} is not supported.")
            };
            const string prefix = "KokoroTTS/Misaki";
            string goldPath = isBritish ? $"{prefix}/gb_gold" : $"{prefix}/us_gold";
            string silverPath = isBritish ? $"{prefix}/gb_silver" : $"{prefix}/us_silver";

            var dicts = await Task.WhenAll(
                LoadDictResource(goldPath, cancellationToken),
                LoadDictResource(silverPath, cancellationToken)
            );
            cancellationToken.ThrowIfCancellationRequested();
            var lexicon = new Lexicon(lang, dicts[0], dicts[1]);

            await Awaitable.MainThreadAsync();
            return lexicon;
        }

        static async Task<Dictionary<string, object>> LoadDictResource(string path, CancellationToken cancellationToken)
        {
            await Awaitable.MainThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var request = Resources.LoadAsync<TextAsset>(path);
            await request;
            var asset = request.asset as TextAsset;
            string text = asset.text;

            // Run heavy process on BG thread
            await Awaitable.BackgroundThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

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
            return GrowDictionary(dict);
        }
    }
}