using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ESpeakNg;
using UnityEngine;
using UnityEngine.Assertions;


namespace Kokoro
{
    /// <summary>
    /// G2P using eSpeak 
    /// Based on Kokoro.js
    /// https://github.com/hexgrad/kokoro/blob/main/kokoro.js/src/phonemize.js
    /// </summary>
    public sealed class ESpeakG2P : IG2P
    {
        readonly StringBuilder sb = new();

        public readonly string DataPath;
        public LanguageCode Language { get; private set; }

        public ESpeakG2P(string dataPath = null)
        {
            DataPath = string.IsNullOrEmpty(dataPath)
                ? Path.Combine(Application.dataPath, "..", "espeak-ng-data")
                : dataPath;
            Assert.IsTrue(Directory.Exists(dataPath), $"eSpeak data directory does not exist: {dataPath}");
        }

        public void Dispose()
        {
            // FIXME: Hangs in the second run?
            // espeak_ERROR result = ESpeak.Terminate();
            // Assert.AreEqual(espeak_ERROR.EE_OK, result, $"Failed to terminate espeak-ng: {result}");
        }

        public Task InitializeAsync(LanguageCode lang, CancellationToken cancellationToken)
        {
            Language = lang;

            const espeakINITIALIZE options = espeakINITIALIZE.espeakINITIALIZE_PHONEME_IPA | espeakINITIALIZE.espeakINITIALIZE_DONT_EXIT;
            ESpeak.Initialize(DataPath, options);

            var status = ESpeak.InitializeOutput(espeak_ng_OUTPUT_MODE.ENOUTPUT_MODE_SYNCHRONOUS, 0, null);
            Assert.AreEqual(espeak_ng_STATUS.ENS_OK, status, $"Failed to initialize output: {status}");

            (string version, string currentPath) = ESpeak.GetInfo();
            Debug.Log($"version: {version}, current path: {currentPath}");

            string langStr = LangToString(lang);
            espeak_ERROR result = ESpeak.SetLanguage(langStr);
            Assert.AreEqual(espeak_ERROR.EE_OK, result, $"Failed to set language: {result}");

            return Task.CompletedTask;
        }

        public string Convert(ReadOnlySpan<char> input)
        {
            // 1. Normalize input
            string text = Normalize(input.ToString());
            // Debug.Log($"input:{input.ToString()} => {text}");

            // 2. Convert to phonemes
            const espeakPhonemesOptions options = ESpeak.DefaultPhonemeOptions;
            const int separator = ESpeak.DefaultPhonemesSeparator;
            const char separatorChar = '\u200d';
            var phonemes = ESpeak.TextToPhonemes(text, options, separator);

            string result = string.Join(' ', phonemes);

            // 4. Post-process phonemes
            result = result
                .Replace("ʲ", "j")
                .Replace("r", "ɹ")
                .Replace("x", "k")
                .Replace("ɬ", "l");
            result = result.Replace($"{separatorChar}", "");

            // Use regex for more complex replacements
            result = Regex.Replace(result, @"(?<=[a-zɹː])(?=hˈʌndɹɪd)", " ");
            result = Regex.Replace(result, @" z(?=[;:,.!?¡¿—…""«»"" ]|$)", "z");

            // Additional post-processing for American English
            if (Language == LanguageCode.En_US)
            {
                result = Regex.Replace(result, @"(?<=nˈaɪn)ti(?!ː)", "di");
            }

            return result.Trim();
        }

        public static string LangToString(LanguageCode lang)
        {
            return lang switch
            {
                LanguageCode.En_US => "en-us",
                LanguageCode.En_GB => "en-gb",
                _ => throw new NotSupportedException($"Language {lang} is not supported"),
            };
        }

        /// <summary>
        /// Helper function to split numbers into phonetic equivalents
        /// </summary>
        /// <param name="match">The matched number</param>
        /// <returns>The phonetic equivalent</returns>
        static string SplitNum(Match match)
        {
            string value = match.Value;

            if (value.Contains("."))
            {
                return value;
            }
            else if (value.Contains(":"))
            {
                string[] parts = value.Split(':');
                int h = int.Parse(parts[0]);
                int m = int.Parse(parts[1]);

                if (m == 0)
                {
                    return $"{h} o'clock";
                }
                else if (m < 10)
                {
                    return $"{h} oh {m}";
                }
                return $"{h} {m}";
            }

            if (value.Length >= 4)
            {
                int year = int.Parse(value.Substring(0, 4));
                if (year < 1100 || year % 1000 < 10)
                {
                    return value;
                }

                string left = value.Substring(0, 2);
                int right = int.Parse(value.Substring(2, 2));
                string suffix = value.EndsWith("s") ? "s" : "";

                if (year % 1000 >= 100 && year % 1000 <= 999)
                {
                    if (right == 0)
                    {
                        return $"{left} hundred{suffix}";
                    }
                    else if (right < 10)
                    {
                        return $"{left} oh {right}{suffix}";
                    }
                }
                return $"{left} {right}{suffix}";
            }

            return value;
        }

        /// <summary>
        /// Helper function to format monetary values
        /// </summary>
        /// <param name="match">The matched currency</param>
        /// <returns>The formatted currency</returns>
        static string FlipMoney(System.Text.RegularExpressions.Match match)
        {
            string value = match.Value;
            string bill = value[0] == '$' ? "dollar" : "pound";

            if (double.TryParse(value.Substring(1), out _) == false)
            {
                return $"{value.Substring(1)} {bill}s";
            }
            else if (!value.Contains("."))
            {
                string suffix = value.Substring(1) == "1" ? "" : "s";
                return $"{value.Substring(1)} {bill}{suffix}";
            }

            string[] parts = value.Substring(1).Split('.');
            string b = parts[0];
            string c = parts[1].PadRight(2, '0');
            int d = int.Parse(c.Substring(0, 2));

            string coins = value[0] == '$'
                ? (d == 1 ? "cent" : "cents")
                : (d == 1 ? "penny" : "pence");

            return $"{b} {bill}{(b == "1" ? "" : "s")} and {d} {coins}";
        }

        /// <summary>
        /// Helper function to process decimal numbers
        /// </summary>
        /// <param name="match">The matched number</param>
        /// <returns>The formatted number</returns>
        static string PointNum(Match match)
        {
            string[] parts = match.Value.Split('.');
            string a = parts[0];
            string b = string.Join(" ", parts[1].ToCharArray());

            return $"{a} point {b}";
        }

        /// <summary>
        /// Normalize text for phonemization
        /// </summary>
        /// <param name="text">The text to normalize</param>
        /// <returns>The normalized text</returns>
        static string Normalize(string text)
        {
            // 1. Handle quotes and brackets
            text = text.Replace("'", "'")
                .Replace("'", "'")
                .Replace("«", "\"")
                .Replace("»", "\"")
                .Replace("\"", "\"")
                .Replace("\"", "\"")
                .Replace("(", "«")
                .Replace(")", "»");

            // 2. Replace uncommon punctuation marks
            text = text.Replace("、", ", ")
                .Replace("。", ". ")
                .Replace("！", "! ")
                .Replace("，", ", ")
                .Replace("：", ": ")
                .Replace("；", "; ")
                .Replace("？", "? ");

            // 3. Whitespace normalization
            text = Regex.Replace(text, @"[^\S \n]", " "); // Replace whitespace except space and newline with space
            text = Regex.Replace(text, @"  +", " ");      // Replace multiple spaces with a single space
            text = Regex.Replace(text, @"(?<=\n) +(?=\n)", ""); // Remove spaces between newlines

            // 4. Abbreviations
            text = Regex.Replace(text, @"\bD[Rr]\.(?= [A-Z])", "Doctor");
            text = Regex.Replace(text, @"\b(?:Mr\.|MR\.(?= [A-Z]))", "Mister");
            text = Regex.Replace(text, @"\b(?:Ms\.|MS\.(?= [A-Z]))", "Miss");
            text = Regex.Replace(text, @"\b(?:Mrs\.|MRS\.(?= [A-Z]))", "Mrs");
            text = Regex.Replace(text, @"\betc\.(?! [A-Z])", "etc");

            // 5. Normalize casual words
            text = Regex.Replace(text, @"\b(y)eah?\b", "$1e'a");

            // 6. Handle numbers and currencies
            text = Regex.Replace(text, @"\d*\.\d+|\b\d{4}s?\b|(?<!:)\b(?:[1-9]|1[0-2]):[0-5]\d\b(?!:)", SplitNum);
            text = Regex.Replace(text, @"(?<=\d),(?=\d)", "");
            text = Regex.Replace(text, @"[$£]\d+(?:\.\d+)?(?: hundred| thousand| (?:[bm]|tr)illion)*\b|[$£]\d+\.\d\d?\b", FlipMoney);
            text = Regex.Replace(text, @"\d*\.\d+", PointNum);
            text = Regex.Replace(text, @"(?<=\d)-(?=\d)", " to ");
            text = Regex.Replace(text, @"(?<=\d)S", " S");

            // 7. Handle possessives
            text = Regex.Replace(text, @"(?<=[BCDFGHJ-NP-TV-Z])'?s\b", "'S");
            text = Regex.Replace(text, @"(?<=X')S\b", "s");

            // 8. Handle hyphenated words/letters
            text = Regex.Replace(text, @"(?:[A-Za-z]\.){2,} [a-z]", m => m.Value.Replace(".", "-"));
            text = Regex.Replace(text, @"(?<=[A-Z])\.(?=[A-Z])", "-");

            // 9. Strip leading and trailing whitespace
            return text.Trim();
        }
    }
}
