using System;
using System.Threading.Tasks;

namespace Kokoro
{
    // Interface for Grapheme to Phoneme (G2P) conversion
    public interface IG2P : IDisposable
    {
        Task InitializeAsync(LanguageCode lang);
        string Convert(ReadOnlySpan<char> text);
    }
}
