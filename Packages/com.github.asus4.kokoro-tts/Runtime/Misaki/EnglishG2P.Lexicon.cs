using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Serialization.Json;
using UnityEngine.Assertions;

namespace Kokoro.Misaki
{
    public partial class EnglishG2P
    {
        // Constants for lexicon operations
        static readonly HashSet<string> ORDINALS = new() { "st", "nd", "rd", "th" };
        static readonly Dictionary<string, (string, string)> CURRENCIES = new()
        {
            { "$", ("dollar", "cent") },
            { "£", ("pound", "pence") },
            { "€", ("euro", "cent") },
        };
        static readonly Dictionary<string, string> ADD_SYMBOLS = new() { { ".", "dot" }, { "/", "slash" } };
        static readonly Dictionary<string, string> SYMBOLS = new()
            { { "%", "percent" }, { "&", "and" }, { "+", "plus" }, { "@", "at" } };

        const string US_VOCAB = "AIOWYbdfhijklmnpstuvwzæðŋɑɔəɛɜɡɪɹɾʃʊʌʒʤʧˈˌθᵊᵻʔ"; // ɐ
        const string GB_VOCAB = "AIQWYabdfhijklmnpstuvwzðŋɑɒɔəɛɜɡɪɹʃʊʌʒʤʧˈˌːθᵊ";  //ɐ

        static readonly HashSet<char> US_TAUS = new() { 'A', 'I', 'O', 'W', 'Y', 'i', 'u', 'æ', 'ɑ', 'ə', 'ɛ', 'ɪ', 'ɹ', 'ʊ', 'ʌ' };

        const string STRESSES = "ˌˈ";
        const string PRIMARY_STRESS = "ˈ";
        const string SECONDARY_STRESS = "ˌ";


        // The Lexicon class for phoneme lookups
        sealed class Lexicon
        {
            private readonly bool british;
            private readonly (float, float) capStresses;
            private readonly Dictionary<string, object> golds;
            private readonly Dictionary<string, object> silvers;
            private readonly HashSet<char> vocab;

            public Lexicon(bool british)
            {
                this.british = british;
                capStresses = (0.5f, 2f);

                string prefix = british ? "gb" : "us";
                var goldJson = Resources.Load<TextAsset>($"KokoroTTS/Misaki/{prefix}_gold");
                var silverJson = Resources.Load<TextAsset>($"KokoroTTS/Misaki/{prefix}_silver");

                golds = JsonSerialization.FromJson<Dictionary<string, object>>(goldJson.text);
                silvers = JsonSerialization.FromJson<Dictionary<string, object>>(silverJson.text);

                static bool IsExpectedType(object o) => o is string || o is Dictionary<string, object>;
                Assert.IsTrue(golds.Values.All(IsExpectedType));
                Assert.IsTrue(silvers.Values.All(IsExpectedType));

                // Grow dictionaries
                golds = GrowDictionary(golds);
                silvers = GrowDictionary(silvers);

                // Select the appropriate vocabulary
                vocab = british
                    ? new HashSet<char>(GB_VOCAB)
                    : new HashSet<char>(US_VOCAB);

                Resources.UnloadAsset(goldJson);
                Resources.UnloadAsset(silverJson);
            }

            // Add capitalized/lowercase versions of words to the dictionary
            static Dictionary<string, object> GrowDictionary(Dictionary<string, object> dict)
            {
                static string Captalize(ReadOnlySpan<char> word)
                    => char.ToUpper(word[0]) + word[1..].ToString();

                var extraDict = new Dictionary<string, object>();
                foreach (var kv in dict)
                {
                    string key = kv.Key;
                    if (key.Length < 2)
                    {
                        continue;
                    }

                    string keyLower = key.ToLower();
                    if (key == keyLower)
                    {
                        string keyCaptalize = Captalize(key);
                        if (key != keyCaptalize)
                        {
                            extraDict[keyCaptalize] = kv.Value;
                        }
                    }
                    else if (key == Captalize(keyLower))
                    {
                        extraDict[keyLower] = kv.Value;
                    }
                }
                foreach (var kv in extraDict)
                {
                    dict[kv.Key] = kv.Value;
                }
                return dict;
            }

            // Get proper noun phonemes
            (string, int?) GetNNP(string word)
            {
                var phoneList = new List<string>();

                foreach (char c in word)
                {
                    if (char.IsLetter(c))
                    {
                        if (golds.TryGetValue(c.ToString().ToUpper(), out object ps))
                        {
                            phoneList.Add(ps.ToString());
                        }
                        else
                        {
                            return (null, null);
                        }
                    }
                }

                string result = ApplyStress(string.Join("", phoneList), 0);

                if (result.Contains(SECONDARY_STRESS))
                {
                    var parts = result.Split(new[] { SECONDARY_STRESS }, StringSplitOptions.RemoveEmptyEntries);
                    result = string.Join(PRIMARY_STRESS, parts);
                }

                return (result, 3);
            }

            // Check for special cases
            private (string, int?) GetSpecialCase(string word, string tag, float? stress, TokenContext ctx)
            {
                if (tag == "ADD" && ADD_SYMBOLS.ContainsKey(word))
                {
                    return Lookup(ADD_SYMBOLS[word], null, -0.5f, ctx);
                }
                else if (SYMBOLS.ContainsKey(word))
                {
                    return Lookup(SYMBOLS[word], null, null, ctx);
                }
                else if (word.Contains('.') && word.Replace(".", "").All(char.IsLetter) &&
                         word.Split('.').Max(s => s.Length) < 3)
                {
                    return GetNNP(word);
                }
                else if (word == "a" || word == "A")
                {
                    return (tag == "DT" ? "ɐ" : "ˈA", 4);
                }
                else if (word == "am" || word == "Am" || word == "AM")
                {
                    if (tag.StartsWith("NN"))
                    {
                        return GetNNP(word);
                    }
                    else if (!ctx.FutureVowel.HasValue || word != "am" ||
                            (stress.HasValue && stress > 0))
                    {
                        return (golds["am"].ToString(), 4);
                    }
                    return ("ɐm", 4);
                }
                else if (word == "an" || word == "An" || word == "AN")
                {
                    if (word == "AN" && tag.StartsWith("NN"))
                    {
                        return GetNNP(word);
                    }
                    return ("ɐn", 4);
                }
                else if (word == "I" && tag == "PRP")
                {
                    return ($"{SECONDARY_STRESS}I", 4);
                }
                else if ((word == "by" || word == "By" || word == "BY") && GetParentTag(tag) == "ADV")
                {
                    return ("bˈI", 4);
                }
                else if ((word == "to" || word == "To" || (word == "TO" && (tag == "TO" || tag == "IN"))))
                {
                    if (!ctx.FutureVowel.HasValue)
                    {
                        return (golds["to"].ToString(), 4);
                    }
                    return (ctx.FutureVowel.Value ? "tʊ" : "tə", 4);
                }
                else if ((word == "in" || word == "In") || (word == "IN" && tag != "NNP"))
                {
                    string stressStr = (ctx.FutureVowel == null || tag != "IN") ? PRIMARY_STRESS : "";
                    return (stressStr + "ɪn", 4);
                }
                else if ((word == "the" || word == "The") || (word == "THE" && tag == "DT"))
                {
                    return (ctx.FutureVowel == true ? "ði" : "ðə", 4);
                }
                else if (tag == "IN" && Regex.IsMatch(word, @"(?i)vs\.?$"))
                {
                    return Lookup("versus", null, null, ctx);
                }
                else if (word == "used" || word == "Used" || word == "USED")
                {
                    if ((tag == "VBD" || tag == "JJ") && ctx.FutureTo)
                    {
                        var usedDict = (Dictionary<string, object>)golds["used"];
                        return (usedDict["VBD"].ToString(), 4);
                    }
                    var usedDictDefault = (Dictionary<string, object>)golds["used"];
                    return (usedDictDefault["DEFAULT"].ToString(), 4);
                }

                return (null, null);
            }

            // Get parent tag category
            static string GetParentTag(string tag)
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

            // Check if word is known in lexicon
            bool IsKnown(string word, string tag)
            {
                if (golds.ContainsKey(word) || SYMBOLS.ContainsKey(word) || silvers.ContainsKey(word))
                {
                    return true;
                }
                else if (!word.All(char.IsLetter) || !word.All(c => char.IsLetterOrDigit(c) || c == '\'' || c == '-'))
                {
                    return false;
                }
                else if (word.Length == 1)
                {
                    return true;
                }
                else if (word == word.ToUpper() && golds.ContainsKey(word.ToLower()))
                {
                    return true;
                }

                return word.Substring(1) == word.Substring(1).ToUpper();
            }

            // Main lookup method
            (string, int?) Lookup(string word, string tag, float? stress, TokenContext ctx)
            {
                bool isNNP = false;

                bool inGold = golds.TryGetValue(word, out object ps);
                if (word == word.ToUpper() && !inGold)
                {
                    word = word.ToLower();
                    isNNP = tag == "NNP";
                }
                int? rating = 4;
                if (!inGold && !isNNP)
                {
                    if (silvers.TryGetValue(word, out ps))
                    {
                        rating = 3;
                    }
                }
                if (ps != null && ps is Dictionary<string, object> options)
                {
                    if (ctx != null && !ctx.FutureVowel.HasValue && options.ContainsKey("None"))
                    {
                        tag = "None";
                    }
                    else if (!options.ContainsKey(tag))
                    {
                        tag = GetParentTag(tag);
                    }
                    if (!options.TryGetValue(tag, out ps))
                    {
                        ps = options["DEFAULT"];
                    }
                }
                if (ps == null || (isNNP && ps is string psStr && psStr.Contains(PRIMARY_STRESS)))
                {
                    (ps, rating) = GetNNP(word);
                    if (ps != null)
                    {
                        return (ps as string, rating);
                    }
                }
                return (ApplyStress(ps as string, stress), rating);
            }

            // Stemming helper for 's' endings
            string _s(string stem)
            {
                if (string.IsNullOrEmpty(stem))
                    return null;

                char last = stem[stem.Length - 1];
                if ("ptkfθ".Contains(last))
                {
                    return stem + "s";
                }
                else if ("szʃʒʧʤ".Contains(last))
                {
                    return stem + (british ? "ɪ" : "ᵻ") + "z";
                }

                return stem + "z";
            }

            // Process words ending with 's'
            (string, int?) StemS(string word, string tag, float? stress, TokenContext ctx)
            {
                if (word.Length < 3 || !word.EndsWith("s"))
                    return (null, null);

                string stem = null;

                if (!word.EndsWith("ss") && IsKnown(word.Substring(0, word.Length - 1), tag))
                {
                    stem = word.Substring(0, word.Length - 1);
                }
                else if ((word.EndsWith("'s") ||
                         (word.Length > 4 && word.EndsWith("es") && !word.EndsWith("ies"))) &&
                         IsKnown(word.Substring(0, word.Length - 2), tag))
                {
                    stem = word.Substring(0, word.Length - 2);
                }
                else if (word.Length > 4 && word.EndsWith("ies") &&
                        IsKnown(word.Substring(0, word.Length - 3) + "y", tag))
                {
                    stem = word.Substring(0, word.Length - 3) + "y";
                }
                else
                {
                    return (null, null);
                }

                var result = Lookup(stem, tag, stress, ctx);
                return (_s(result.Item1), result.Item2);
            }

            // Stemming helper for 'ed' endings
            string _ed(string stem)
            {
                if (string.IsNullOrEmpty(stem))
                    return null;

                char last = stem[stem.Length - 1];
                if ("pkfθʃsʧ".Contains(last))
                {
                    return stem + "t";
                }
                else if (last == 'd')
                {
                    return stem + (british ? "ɪ" : "ᵻ") + "d";
                }
                else if (last != 't')
                {
                    return stem + "d";
                }
                else if (british || stem.Length < 2)
                {
                    return stem + "ɪd";
                }
                else if (US_TAUS.Contains(stem[stem.Length - 2]))
                {
                    return stem.Substring(0, stem.Length - 1) + "ɾᵻd";
                }

                return stem + "ᵻd";
            }

            // Process words ending with 'ed'
            (string, int?) StemEd(string word, string tag, float? stress, TokenContext ctx)
            {
                if (word.Length < 4 || !word.EndsWith("d"))
                    return (null, null);

                string stem = null;

                if (!word.EndsWith("dd") && IsKnown(word.Substring(0, word.Length - 1), tag))
                {
                    stem = word.Substring(0, word.Length - 1);
                }
                else if (word.Length > 4 && word.EndsWith("ed") && !word.EndsWith("eed") &&
                        IsKnown(word.Substring(0, word.Length - 2), tag))
                {
                    stem = word.Substring(0, word.Length - 2);
                }
                else
                {
                    return (null, null);
                }

                var result = Lookup(stem, tag, stress, ctx);
                return (_ed(result.Item1), result.Item2);
            }

            // Stemming helper for 'ing' endings
            string _ing(string stem)
            {
                if (string.IsNullOrEmpty(stem))
                    return null;

                if (british)
                {
                    if ("əː".Contains(stem[stem.Length - 1]))
                    {
                        return null;
                    }
                }
                else if (stem.Length > 1 && stem[stem.Length - 1] == 't' &&
                        US_TAUS.Contains(stem[stem.Length - 2]))
                {
                    return stem.Substring(0, stem.Length - 1) + "ɾɪŋ";
                }

                return stem + "ɪŋ";
            }

            // Process words ending with 'ing'
            (string, int?) StemIng(string word, string tag, float? stress, TokenContext ctx)
            {
                if (word.Length < 5 || !word.EndsWith("ing"))
                    return (null, null);

                string stem = null;

                if (word.Length > 5 && IsKnown(word.Substring(0, word.Length - 3), tag))
                {
                    stem = word.Substring(0, word.Length - 3);
                }
                else if (IsKnown(word.Substring(0, word.Length - 3) + "e", tag))
                {
                    stem = word.Substring(0, word.Length - 3) + "e";
                }
                else if (word.Length > 5 && Regex.IsMatch(word, @"([bcdgklmnprstvxz])\1ing$|cking$") &&
                        IsKnown(word.Substring(0, word.Length - 4), tag))
                {
                    stem = word.Substring(0, word.Length - 4);
                }
                else
                {
                    return (null, null);
                }

                var result = Lookup(stem, tag, stress.HasValue ? 0.5f : stress, ctx);
                return (_ing(result.Item1), result.Item2);
            }

            // Check if string is a digit
            static bool IsDigit(string text)
            {
                return Regex.IsMatch(text, @"^[0-9]+$");
            }

            // Check if word is a number
            static bool IsNumber(string word, bool isHead)
            {
                if (!word.Any(c => IsDigit(c.ToString())))
                    return false;

                // Check for suffixes
                string[] suffixes = new[] { "ing", "'d", "ed", "'s" };
                suffixes = suffixes.Concat(ORDINALS).Append("s").ToArray();

                foreach (string s in suffixes)
                {
                    if (word.EndsWith(s))
                    {
                        word = word.Substring(0, word.Length - s.Length);
                        break;
                    }
                }

                return word.All(c =>
                    IsDigit(c.ToString()) ||
                    c == ',' ||
                    c == '.' ||
                    (isHead && c == '-')
                );
            }

            // Main entry point for word processing
            public (string, int?) GetWord(MToken token, string tag, int? stress, TokenContext ctx)
            {
                // Normalize text
                string word = token.Text;

                // Apply stress based on capitalization
                float? wordStress = null;
                if (word != word.ToLower())
                {
                    wordStress = word == word.ToUpper() ? capStresses.Item2 : capStresses.Item1;
                }

                // Try special cases first
                var specialCase = GetSpecialCase(word, tag, wordStress, ctx);
                if (specialCase.Item1 != null)
                {
                    return specialCase;
                }

                // Try lowercase for capitalized words
                string wl = word.ToLower();
                if (word.Length > 1 &&
                    Regex.IsMatch(word.Replace("'", ""), @"^[A-Za-z]+$") &&
                    word != word.ToLower() &&
                    (tag != "NNP" || word.Length > 7) &&
                    !golds.ContainsKey(word) &&
                    !silvers.ContainsKey(word) &&
                    (word == word.ToUpper() || word.Substring(1) == word.Substring(1).ToLower()) &&
                    (golds.ContainsKey(wl) || silvers.ContainsKey(wl) ||
                     StemS(wl, tag, wordStress, ctx).Item1 != null ||
                     StemEd(wl, tag, wordStress, ctx).Item1 != null ||
                     StemIng(wl, tag, wordStress, ctx).Item1 != null))
                {
                    word = wl;
                }

                // Regular dictionary lookup
                if (IsKnown(word, tag))
                {
                    return Lookup(word, tag, wordStress, ctx);
                }
                else if (word.EndsWith("s'") && IsKnown(word.Substring(0, word.Length - 2) + "'s", tag))
                {
                    return Lookup(word.Substring(0, word.Length - 2) + "'s", tag, wordStress, ctx);
                }
                else if (word.EndsWith("'") && IsKnown(word.Substring(0, word.Length - 1), tag))
                {
                    return Lookup(word.Substring(0, word.Length - 1), tag, wordStress, ctx);
                }

                // Try stemming
                var stemS = StemS(word, tag, wordStress, ctx);
                if (stemS.Item1 != null)
                {
                    return stemS;
                }

                var stemEd = StemEd(word, tag, wordStress, ctx);
                if (stemEd.Item1 != null)
                {
                    return stemEd;
                }

                var stemIng = StemIng(word, tag, wordStress.HasValue ? 0.5f : wordStress, ctx);
                if (stemIng.Item1 != null)
                {
                    return stemIng;
                }

                // No match found
                return (null, null);
            }

            // Apply stress to phonemes
            static string ApplyStress(string ps, float? stress)
            {
                if (stress == null || string.IsNullOrEmpty(ps))
                {
                    return ps;
                }

                if (stress < -1)
                {
                    return ps.Replace(PRIMARY_STRESS, "").Replace(SECONDARY_STRESS, "");
                }
                else if (stress == -1 || (stress == 0 || stress == -0.5f) && ps.Contains(PRIMARY_STRESS))
                {
                    return ps.Replace(SECONDARY_STRESS, "").Replace(PRIMARY_STRESS, SECONDARY_STRESS);
                }
                else if ((stress == 0 || stress == 0.5f || stress == 1) &&
                        !ps.Contains(PRIMARY_STRESS) && !ps.Contains(SECONDARY_STRESS))
                {
                    if (!ps.Any(c => VOWELS.Contains(c)))
                    {
                        return ps;
                    }
                    return RestressPhonemes(SECONDARY_STRESS + ps);
                }
                else if (stress >= 1 && !ps.Contains(PRIMARY_STRESS) && ps.Contains(SECONDARY_STRESS))
                {
                    return ps.Replace(SECONDARY_STRESS, PRIMARY_STRESS);
                }
                else if (stress > 1 && !ps.Contains(PRIMARY_STRESS) && !ps.Contains(SECONDARY_STRESS))
                {
                    if (!ps.Any(c => VOWELS.Contains(c)))
                    {
                        return ps;
                    }
                    return RestressPhonemes(PRIMARY_STRESS + ps);
                }
                return ps;
            }

            // Helper method for restressing phonemes
            static string RestressPhonemes(string ps)
            {
                // Create a list of index-phoneme pairs
                var ips = new List<(int, char)>();
                for (int i = 0; i < ps.Length; i++)
                {
                    ips.Add((i, ps[i]));
                }

                // Find stress positions
                var stresses = new Dictionary<int, int>();
                for (int i = 0; i < ips.Count; i++)
                {
                    if (STRESSES.Contains(ips[i].Item2))
                    {
                        // Find the next vowel position
                        int j = i;
                        while (j < ips.Count && !VOWELS.Contains(ips[j].Item2))
                        {
                            j++;
                        }

                        if (j < ips.Count)
                        {
                            stresses[i] = j;
                        }
                    }
                }

                // Adjust positions
                foreach (var kvp in stresses)
                {
                    int i = kvp.Key;
                    int j = kvp.Value;
                    char s = ips[i].Item2;
                    ips[i] = ((int)(j - 0.5), s);
                }

                // Sort and reconstruct the string
                return string.Join("", ips.OrderBy(p => p.Item1).Select(p => p.Item2));
            }
        }
    }
}
