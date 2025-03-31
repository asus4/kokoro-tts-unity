using System;
using System.Threading.Tasks;
using System.IO;
using NUnit.Framework;
using Kokoro.Misaki;
using UnityEngine;
using Unity.Serialization.Json;

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
        const string DATA_DIR = "Packages/com.github.asus4.kokoro-tts/Tests/Data/";

        [TestCase(LanguageCode.En_US, "american_test_data.json")]
        [TestCase(LanguageCode.En_GB, "british_test_data.json")]
        public async Task TestEnglishG2P(LanguageCode lang, string fileName)
        {
            string filePath = Path.Combine(DATA_DIR, fileName);
            string json = await File.ReadAllTextAsync(filePath);
            var testData = JsonUtility.FromJson<TestData>(json);
            Assert.That(testData.data, Is.Not.Empty, "No test data found");

            using var g2p = new EnglishG2P(lang);
            foreach (var item in testData.data)
            {
                var phonemes = g2p.Convert(item.text);
                Assert.That(phonemes, Is.EqualTo(item.phonemes), $"Phoneme mismatch for text: {item.text}");
            }
        }
    }
}
