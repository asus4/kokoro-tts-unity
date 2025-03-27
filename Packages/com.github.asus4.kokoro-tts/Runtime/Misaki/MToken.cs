#nullable enable

namespace Kokoro.Misaki
{
    public record MToken
    {
        public string Text { get; internal set; }
        public string Tag { get; internal set; }
        public string WhiteSpace { get; internal set; }
        public string Phonomes { get; internal set; }
        public float? StartTS { get; internal set; }
        public float? EndTS { get; internal set; }

        public MToken(
            string text, string tag, string whiteSpace, string phonomes,
            float? startTS, float? endTS)
        {
            Text = text;
            Tag = tag;
            WhiteSpace = whiteSpace;
            Phonomes = phonomes;
            StartTS = startTS;
            EndTS = endTS;
        }
    }
}
