using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
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

        public ESpeakG2P(string dataPath)
        {
            DataPath = dataPath;
            Assert.IsTrue(Directory.Exists(dataPath), $"eSpeak data directory does not exist: {dataPath}");
        }

        public void Dispose()
        {
            // FIXME: Hangs in the second run?
            // espeak_ERROR result = ESpeak.Terminate();
            // Assert.AreEqual(espeak_ERROR.EE_OK, result, $"Failed to terminate espeak-ng: {result}");
        }

        public Task InitializeAsync(LanguageCode lang)
        {
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

        public string Convert(ReadOnlySpan<char> text)
        {
            const espeakPhonemesOptions options = ESpeak.DefaultPhonemeOptions;
            const int separator = ESpeak.DefaultPhonemesSeparator;
            // const char separatorChar = '\u200d';

            var phonemes = ESpeak.TextToPhonemes(text, options, separator);
            return string.Join(' ', phonemes);
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
    }
}
