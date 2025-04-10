using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kokoro
{
    // Interface for Grapheme to Phoneme (G2P) conversion
    public interface IG2P : IDisposable
    {
        Task InitializeAsync(LanguageCode lang, CancellationToken cancellationToken);
        string Convert(ReadOnlySpan<char> text);
    }
}
