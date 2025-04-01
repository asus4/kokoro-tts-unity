using System;
using System.Threading.Tasks;
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
            string dataPath = Path.Combine(Application.streamingAssetsPath, "espeak-ng-data");
            Assert.IsTrue(Directory.Exists(dataPath), $"espeak-ng data directory does not exist: {dataPath}");

            ESpeak.InitializePath(dataPath);
        }

        [TearDown]
        public void Teardown()
        {
            espeak_ERROR result = ESpeak.Terminate();
            Assert.AreEqual(espeak_ERROR.EE_OK, result, $"Failed to terminate espeak-ng: {result}");
        }

        [Test]
        public void GetInfoTest()
        {
            (string version, string currentPath) = ESpeak.GetInfo();
            Debug.Log($"version: {version}, current path: {currentPath}");
            Assert.IsNotEmpty(version, $"Failed to get espeak-ng version");
            Assert.IsNotEmpty(currentPath, $"Failed to get espeak-ng data-path");
        }

        [TestCase("american_test_data.json", "aaaa")]
        public async Task TextToPhonemesTest(string input, string expected)
        {
            // result = ESpeak.SetVoiceByFile(Path.Combine(Application.streamingAssetsPath, "espeak-ng-data/voices/!v/Alex"));
            // Assert.AreEqual(espeak_ERROR.EE_OK, result, $"Failed to set voice: {result}");

            // string text = "Hello, world!";
            // string phonemes = ESpeak.TextToPhonemes(text, 0);
            // Debug.Log($"text: {text}, phonemes: {phonemes}");
        }
    }
}
