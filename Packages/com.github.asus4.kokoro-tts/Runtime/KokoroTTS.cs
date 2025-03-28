using System;
using System.Threading;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Unity;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Networking;

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

        public static async Awaitable<KokoroTTS> CreateAsync(byte[] modelData, Options options, CancellationToken cancellationToken)
        {
            await Awaitable.BackgroundThreadAsync();
            var instance = new KokoroTTS(modelData, options);
            await Awaitable.MainThreadAsync();
            if (cancellationToken.IsCancellationRequested)
            {
                instance?.Dispose();
                throw new OperationCanceledException(cancellationToken);
            }
            return instance;
        }

        public async Awaitable LoadVoiceAsync(Uri uri, CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(uri);
            await request.SendWebRequest();
            cancellationToken.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"Failed to load voice: {request.error}");
            }
        }
    }
}
