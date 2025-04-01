using System;
using System.Threading.Tasks;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ESpeakNg.Tests
{

    [TestFixture]
    public class G2PTests
    {
        const string DATA_DIR = "Packages/com.github.asus4.kokoro-tts/Tests/Data/";


        [Test]
        public void NativeMethodTest()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "espeak-ng-data");
            Assert.IsTrue(Directory.Exists(path), $"espeak-ng data directory does not exist: {path}");

            // ESpeak.InitializePath(path);


            espeakINITIALIZE frag = espeakINITIALIZE.espeakINITIALIZE_DONT_EXIT | espeakINITIALIZE.espeakINITIALIZE_PHONEME_IPA;
            ESpeak.Initialize(path, frag);


            string version = ESpeak.GetInfo(out string currentPath);
            Debug.Log($"version: {version}, current path: {currentPath}");
            Assert.IsNotNull(version, $"Failed to get espeak-ng info: {version}");


            espeak_ERROR result;
            // result = ESpeak.SetVoiceByFile(Path.Combine(Application.streamingAssetsPath, "espeak-ng-data/voices/!v/Alex"));
            // Assert.AreEqual(espeak_ERROR.EE_OK, result, $"Failed to set voice: {result}");

            // string text = "Hello, world!";
            // string phonemes = ESpeak.TextToPhonemes(text, 0);
            // Debug.Log($"text: {text}, phonemes: {phonemes}");

            result = ESpeak.Terminate();
            Assert.AreEqual(espeak_ERROR.EE_OK, result, $"Failed to terminate espeak-ng: {result}");
        }

        [TestCase("american_test_data.json")]
        public async Task TestEnglishG2P(string fileName)
        {
            string filePath = Path.Combine(DATA_DIR, fileName);
            string json = await File.ReadAllTextAsync(filePath);
            Debug.Log(filePath);
        }
    }
}
