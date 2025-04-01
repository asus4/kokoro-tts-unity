using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ESpeakNg.Tests
{

    [TestFixture]
    public class PhonemesTest
    {
        [SetUp]
        public void Setup()
        {
            string dataPath = Path.Combine(Application.dataPath, "..", "espeak-ng-data");
            dataPath = Path.GetFullPath(dataPath);
            Assert.IsTrue(Directory.Exists(dataPath), $"espeak-ng data directory does not exist: {dataPath}");

            // ESpeak.InitializePath(dataPath);

            var options
                // = espeakINITIALIZE.espeakINITIALIZE_PHONEME_EVENTS
                = espeakINITIALIZE.espeakINITIALIZE_PHONEME_IPA
                // | espeakINITIALIZE.espeakPHONEMES_TIE
                | espeakINITIALIZE.espeakINITIALIZE_DONT_EXIT;
            int Hz = ESpeak.Initialize(dataPath, options);
            Debug.Log($"espeak-ng initialized with Hz: {Hz}");

            var result = ESpeak.InitializeOutput(espeak_ng_OUTPUT_MODE.ENOUTPUT_MODE_SYNCHRONOUS, 0, null);
            Assert.AreEqual(espeak_ng_STATUS.ENS_OK, result, $"Failed to initialize output: {result}");
        }

        [TearDown]
        public void Teardown()
        {
            // FIXME: Hangs in the second run?
            // espeak_ERROR result = ESpeak.Terminate();
            // Assert.AreEqual(espeak_ERROR.EE_OK, result, $"Failed to terminate espeak-ng: {result}");
        }

        [Test]
        public void GetInfoTest()
        {
            (string version, string currentPath) = ESpeak.GetInfo();
            Debug.Log($"version: {version}, current path: {currentPath}");
            Assert.IsNotEmpty(version, $"Failed to get espeak-ng version");
            Assert.IsNotEmpty(currentPath, $"Failed to get espeak-ng data-path");
        }

        [TestCase("en-us", true)]
        [TestCase("en-gb", true)]
        [TestCase("en", true)]
        [TestCase("ja", true)]
        [TestCase("not-exist", false)]
        public void SetLanguageTest(string language, bool expectedSuccess)
        {
            espeak_ERROR result = ESpeak.SetLanguage(language);
            if (expectedSuccess)
            {
                Assert.AreEqual(espeak_ERROR.EE_OK, result, $"Failed to set language:{language} result, {result}");
            }
            else
            {
                Assert.AreEqual(espeak_ERROR.EE_NOT_FOUND, result, $"Expected failure to set language:{language} result, {result}");
            }
        }


        // https://github.com/xenova/phonemizer.js/blob/main/tests/phonemize.test.js
        [TestCase("en-gb", "Hello, world!", "həlˈə‍ʊ", "wˈɜːld")]
        [TestCase("en-gb", "hi and bye", "hˈa‍ɪ and bˈa‍ɪ")]
        [TestCase("en-gb", "Hi. Bye.", "hˈa‍ɪ", "bˈa‍ɪ")]
        [TestCase("ja", "あいうえお", "aaaa")]
        public void TextToPhonemesTest(string language, string input, params string[] expected)
        {
            espeak_ERROR result = ESpeak.SetLanguage(language);
            Assert.AreEqual(espeak_ERROR.EE_OK, result, $"Failed to set language: {result}");

            var phonemes = ESpeak.TextToPhonemes(input, 3);

            Assert.AreEqual(expected.Length, phonemes.Count, $"Phoneme count mismatch for input: {input}");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], phonemes[i], $"Phoneme mismatch at index {i} for input: {input}");
            }
        }
    }
}
