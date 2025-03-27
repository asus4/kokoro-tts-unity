#nullable enable

namespace Kokoro.Misaki
{
    public class UnderscoreData
    {
        public bool IsHead { get; set; } = true;
        public string Alias { get; set; } = null;
        public int? Stress { get; set; } = null;
        public string Currency { get; set; } = null;
        public string NumFlags { get; set; } = "";
        public bool PreSpace { get; set; } = false;
        public int? Rating { get; set; } = null;
    }

    public record MToken
    {
        public string Text { get; internal set; }
        public string Tag { get; internal set; }
        public string WhiteSpace { get; internal set; }
        public string Phonomes { get; internal set; }
        public float? StartTS { get; internal set; }
        public float? EndTS { get; internal set; }

        public UnderscoreData? _ { get; internal set; } = new UnderscoreData();

        public MToken(
            string text, string tag, string whiteSpace, string phonemes,
            float? startTS = null, float? endTS = null)
        {
            Text = text;
            Tag = tag;
            WhiteSpace = whiteSpace;
            Phonomes = phonemes;
            StartTS = startTS;
            EndTS = endTS;
        }
    }
}
