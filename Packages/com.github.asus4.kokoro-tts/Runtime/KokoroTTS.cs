using System;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Unity;
using Unity.Profiling;
using UnityEngine;

namespace Kokoro
{
    public sealed class KokoroTTS : TextToSpeechInference
    {
        [Serializable]
        public class Options : TextToSpeechOptions
        {
        }

        public KokoroTTS(byte[] modelData, Options options) : base(modelData, options)
        {

        }
    }
}
