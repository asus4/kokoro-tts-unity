using System;

namespace Kokoro.Misaki
{
    public enum LanguageCode
    {
        En_US,
        En_GB,
    }


    public interface IG2P
    {
        (string, ReadOnlyMemory<MToken>) Convert(string text);
    }
}
