using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kokoro.Misaki;
using NUnit.Framework;
using UnityEngine;

namespace Kokoro.Tests
{
    /// <summary>
    /// Tests lexicon loading and vocab.
    /// </summary>
    [TestFixture]
    [TestOf(typeof(Lexicon))]
    public class MisakiEnLexiconTests1
    {
        [TestCase(LanguageCode.En_US)]
        [TestCase(LanguageCode.En_GB)]
        public async Task LexiconVocabTest(LanguageCode lang)
        {
            var lexicon = await Lexicon.CreateAsync(lang, default);
            Assert.IsNotNull(lexicon);

            var golds = lexicon._golds;
            var silvers = lexicon._silvers;

            Debug.Log($"Gold : {golds.Count}, Silver : {silvers.Count}");
            Assert.IsTrue(golds.Count > 0);
            Assert.IsTrue(silvers.Count > 0);

            IsAllCharInVocab(lexicon.Vocab, golds);
            IsAllCharInVocab(lexicon.Vocab, silvers);
        }

        static void IsAllCharInVocab(HashSet<char> vocab, Dictionary<string, object> dict)
        {
            var texts = dict.Values.Select(value =>
            {
                if (value is string str)
                {
                    return str;
                }
                if (value is Dictionary<string, string> subDict)
                {
                    return string.Join("", subDict.Values);
                }
                throw new Exception($"Unexpected type: {value.GetType()}");
            });
            foreach (ReadOnlySpan<char> text in texts)
            {
                IsAllCharInVocab(vocab, text);
            }
        }

        static void IsAllCharInVocab(HashSet<char> vocab, ReadOnlySpan<char> text)
        {
            foreach (var c in text)
            {
                if (!vocab.Contains(c))
                {
                    Assert.Fail($"Character '{c}' not included in vocab.");
                }
            }
        }
    }

    [TestFixture]
    [TestOf(typeof(Lexicon))]
    public class MisakiEnLexiconTests2
    {
        Lexicon lexiconEnUS;
        Lexicon lexiconEnGB;
        TokenContext context;

        [SetUp]
        public async Task SetUp()
        {
            lexiconEnUS = await Lexicon.CreateAsync(LanguageCode.En_US, default);
            lexiconEnGB = await Lexicon.CreateAsync(LanguageCode.En_GB, default);
            context = new TokenContext();

            Assert.IsNotNull(lexiconEnUS);
            Assert.IsNotNull(lexiconEnGB);
        }

        [TearDown]
        public void TearDown()
        {
            // Nosing to do for now
        }

        [TestCase(LanguageCode.En_US, "Hello", "həlˈO")]
        public void SimpleTest(LanguageCode code, string input, string expected)
        {
            Lexicon lexicon = code == LanguageCode.En_US ? lexiconEnUS : lexiconEnGB;
            var token = new MToken(input, null, null);
            var result = lexicon[token, context];

            Assert.AreEqual(expected, result.Ps);
        }
    }
}
