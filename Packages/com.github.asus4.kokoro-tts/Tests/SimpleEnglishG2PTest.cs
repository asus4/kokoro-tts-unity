using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Kokoro.Tests
{
    [TestFixture]
    [TestOf(typeof(SimpleEnglishG2P))]
    public sealed class SimpleEnglishG2PTest
    {
        static IG2P g2pEnUS;
        static IG2P g2pEnGB;

        // OneTimeSetUp does not work?
        [SetUp]
        public async Task Setup()
        {
            if (g2pEnUS != null && g2pEnGB != null)
            {
                return;
            }

            g2pEnUS = new SimpleEnglishG2P()
            {
                Verbose = true,
            };
            g2pEnGB = new SimpleEnglishG2P()
            {
                Verbose = true,
            };

            try
            {
                await g2pEnUS.InitializeAsync(LanguageCode.En_US, default);
                await g2pEnGB.InitializeAsync(LanguageCode.En_GB, default);
            }
            catch (Exception e)
            {
                throw e;
            }
        }


        [TestCase(LanguageCode.En_US, "Hello", "həlˈO")]
        [TestCase(LanguageCode.En_GB, "Hello", "həlˈQ")]
        [TestCase(LanguageCode.En_US, "chocolates", "ʧˈɑkᵊlətz")]
        [TestCase(LanguageCode.En_US, "Cat's tail", "kˈæts tˈAl")]
        [TestCase(LanguageCode.En_US, "X's mark", "ˈɛksᵻz mˈɑɹk")]
        [TestCase(LanguageCode.En_US, "washed", "wˌɔʃt")]
        public void SimpleTest(LanguageCode lang, string input, string expected)
        {
            var g2p = lang switch
            {
                LanguageCode.En_US => g2pEnUS,
                LanguageCode.En_GB => g2pEnGB,
                _ => throw new NotImplementedException($"Language {lang} is not supported.")
            };
            var result = g2p.Convert(input);
            Assert.AreEqual(expected, result);
        }

        [TestCase(LanguageCode.En_US, "american_test_data.json")]
        [TestCase(LanguageCode.En_GB, "british_test_data.json")]
        public async Task TestSimpleG2P(LanguageCode lang, string fileName)
        {
            var g2p = lang switch
            {
                LanguageCode.En_US => g2pEnUS,
                LanguageCode.En_GB => g2pEnGB,
                _ => throw new NotImplementedException($"Language {lang} is not supported.")
            };

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
            var testData = UnityEngine.JsonUtility.FromJson<TestData>(json);
            Assert.That(testData.data, Is.Not.Empty, "No test data found");

            return testData;
        }
    }
}
