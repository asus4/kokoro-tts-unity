using System;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Unity;
using UnityEngine;

namespace Kokoro
{
    public class KokoroTTS : IDisposable
    {
        [SerializeField]
        public class Options
        {
            public ExecutionProviderOptions executionProvider;
        }

        public KokoroTTS(byte[] modelData, Options options = null)
        {
            Debug.Log("KokoroTTS constructor");
        }

        public void Dispose()
        {
            Debug.Log("KokoroTTS Dispose");
        }
    }
}
