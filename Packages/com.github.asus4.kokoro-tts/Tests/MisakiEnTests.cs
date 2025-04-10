using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kokoro.Misaki;
using NUnit.Framework;
using UnityEngine;

namespace Kokoro.Tests
{
    [TestFixture]
    public class MisakiEnTests
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
}
