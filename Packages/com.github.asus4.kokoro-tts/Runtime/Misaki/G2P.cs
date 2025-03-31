using System;

namespace Kokoro.Misaki
{


    public interface IG2P
    {
        (string, ReadOnlyMemory<MToken>) Convert(string text);

    }
}
