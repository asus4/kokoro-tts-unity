using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ESpeakNg.Tests
{

    [TestFixture]
    public class NativeMethodsTest
    {
        [SetUp]
        public void Setup()
        {
            string dataPath = Path.Combine(Application.dataPath, "..", "espeak-ng-data");
            dataPath = Path.GetFullPath(dataPath);
            Assert.IsTrue(Directory.Exists(dataPath), $"espeak-ng data directory does not exist: {dataPath}");

            // ESpeak.InitializePath(dataPath);

            var options = espeakINITIALIZE.espeakINITIALIZE_PHONEME_IPA | espeakINITIALIZE.espeakINITIALIZE_DONT_EXIT;
            int Hz = ESpeak.Initialize(dataPath, options);
            Debug.Log($"espeak-ng initialized with Hz: {Hz}");
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

        [TestCase("Hello World", "aaaa", "en-us")]
        [TestCase("こんにちは 世界", "aaaa", "ja")]
        public void TextToPhonemesTest(string input, string expected, string language)
        {
            espeak_ERROR result = ESpeak.SetLanguage(language);
            Assert.AreEqual(espeak_ERROR.EE_OK, result, $"Failed to set language: {result}");

            string phonemes = ESpeak.TextToPhonemes(input, 1);
            Assert.AreEqual(expected, phonemes);
        }
    }
}
