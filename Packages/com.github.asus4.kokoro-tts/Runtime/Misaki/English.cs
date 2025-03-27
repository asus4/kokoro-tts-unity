using System;

namespace Kokoro.Misaki
{
    public class EnglishG2P : IG2P
    {
        public (string, ReadOnlyMemory<MToken>) Convert(string text)
        {
            return (text, new MToken[0]);
        }
    }
}
