using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Kokoro.Tests
{
    [TestFixture]
    [TestOf(typeof(SimpleEnglishG2P))]
    public sealed class SimpleEnglishG2PTest
    {
        IG2P g2pEnUS;
        IG2P g2pEnGB;

        [SetUp]
        public async Task Setup()
        {
            g2pEnUS = new SimpleEnglishG2P();
            g2pEnGB = new SimpleEnglishG2P();
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
        [TestCase(LanguageCode.En_GB, "Hello", "həlˈO")]
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
    }
}
