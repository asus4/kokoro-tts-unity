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
    public partial class EnglishG2P
    {
        // The Lexicon class for phoneme lookups
        sealed class Lexicon
        {
            readonly Dictionary<string, object> golds;
            readonly Dictionary<string, object> silvers;

            public Lexicon(bool british)
            {
                string prefix = british ? "gb" : "us";
                var goldJson = Resources.Load<TextAsset>($"KokoroTTS/Misaki/{prefix}_gold");
                var silverJson = Resources.Load<TextAsset>($"KokoroTTS/Misaki/{prefix}_silver");

                golds = JsonSerialization.FromJson<Dictionary<string, object>>(goldJson.text);
                silvers = JsonSerialization.FromJson<Dictionary<string, object>>(silverJson.text);

                Debug.Log($"Loaded Lexicon: {prefix}");
                Debug.Log($"Golds: {golds.Count}, Silvers: {silvers.Count}");

                Resources.UnloadAsset(goldJson);
                Resources.UnloadAsset(silverJson);
            }

            public (string, int?) GetWord(MToken token, string tag, int? stress, TokenContext ctx)
            {
                string word = token.Text;

                // Check if word exists in dictionaries
                if (golds.TryGetValue(word.ToLower(), out object value))
                {
                    if (value is string phonemes)
                    {
                        return (ApplyStress(phonemes, stress), 4);
                    }
                    else if (value is Dictionary<string, string> options)
                    {
                        string key = "DEFAULT";
                        if (ctx.FutureVowel.HasValue)
                        {
                            key = ctx.FutureVowel.Value.ToString();
                        }

                        if (options.TryGetValue(key, out phonemes))
                        {
                            return (ApplyStress(phonemes, stress), 4);
                        }
                    }
                }

                // Fallback to simple phonemes for demo
                return (null, null);
            }

            // Apply stress to phonemes
            private string ApplyStress(string phonemes, int? stress)
            {
                if (stress == null || string.IsNullOrEmpty(phonemes))
                {
                    return phonemes;
                }

                // This is a simplified version, real implementation would be more complex
                const string PRIMARY_STRESS = "ˈ";
                const string SECONDARY_STRESS = "ˌ";

                if (stress < 0)
                {
                    return phonemes
                        .Replace(PRIMARY_STRESS, "")
                        .Replace(SECONDARY_STRESS, "");
                }
                else if (stress == 0)
                {
                    return phonemes
                        .Replace(PRIMARY_STRESS, SECONDARY_STRESS);
                }

                return phonemes;
            }
        }
    }
}
