using System;
using System.Threading.Tasks;
using System.IO;
using Kokoro.Misaki;
using NUnit.Framework;
using UnityEngine;

namespace Kokoro.Tests
{
    [Serializable]
    internal class TestData
    {
        public Item[] data;

        [Serializable]
        internal class Item
        {
            public string text;
            public string phonemes;
        }
    }

    [TestFixture]
    public class G2PTests
    {

        [TestCase(LanguageCode.En_US, "american_test_data.json")]
        [TestCase(LanguageCode.En_GB, "british_test_data.json")]
        public async Task TestMisakiEn(LanguageCode lang, string fileName)
        {
            using var g2p = new MisakiEnglishG2P();
            await g2p.InitializeAsync(lang, default);

            var testData = await LoadTestData(fileName);
            foreach (var item in testData.data)
            {
                var phonemes = g2p.Convert(item.text);
                Assert.That(phonemes, Is.EqualTo(item.phonemes), $"Phoneme mismatch for text: {item.text}");
            }
        }

        [TestCase(LanguageCode.En_US, "american_test_data.json")]
        [TestCase(LanguageCode.En_GB, "british_test_data.json")]
        public async Task TestESpeakG2P(LanguageCode lang, string fileName)
        {
            using var g2p = new ESpeakG2P();
            await g2p.InitializeAsync(lang, default);

            var testData = await LoadTestData(fileName);
            foreach (var item in testData.data)
            {
                var phonemes = g2p.Convert(item.text);
                Assert.That(phonemes, Is.EqualTo(item.phonemes), $"Phoneme mismatch for text: {item.text}");
            }
        }

        [TestCase(LanguageCode.En_US, "american_test_data.json")]
        [TestCase(LanguageCode.En_GB, "british_test_data.json")]
        public async Task TestSimpleG2P(LanguageCode lang, string fileName)
        {
            using var g2p = new SimpleEnglishG2P();
            await g2p.InitializeAsync(lang, default);

            var testData = await LoadTestData(fileName);
            foreach (var item in testData.data)
            {
                var phonemes = g2p.Convert(item.text);
                Assert.That(phonemes, Is.EqualTo(item.phonemes), $"Phoneme mismatch for text: {item.text}");
            }
        }

        static async Task<TestData> LoadTestData(string fileName)
        {
            const string DATA_DIR = "Packages/com.github.asus4.kokoro-tts/Tests/Data/";
            string filePath = Path.Combine(DATA_DIR, fileName);
            string json = await File.ReadAllTextAsync(filePath);
            var testData = JsonUtility.FromJson<TestData>(json);
            Assert.That(testData.data, Is.Not.Empty, "No test data found");

            return testData;
        }
    }
}
