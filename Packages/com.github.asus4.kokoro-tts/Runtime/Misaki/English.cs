using System;

namespace Kokoro.Misaki
{
    public class EnglishG2P : IG2P, IDisposable
    {
        public LanguageCode Lang { get; }

        public EnglishG2P(LanguageCode lang)
        {
            Lang = lang;
        }

        public (string, ReadOnlyMemory<MToken>) Convert(string text)
        {
            return (text, new MToken[0]);
        }

        public void Dispose()
        {
            // Dispose of any resources if necessary
        }
    }
}
