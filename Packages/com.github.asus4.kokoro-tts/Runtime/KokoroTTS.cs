#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Unity;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;

namespace Kokoro
{

    /// <summary>
    /// Kokoro TTS
    ///
    /// https://github.com/hexgrad/kokoro 
    /// https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX
    /// Licensed under Apache License 2.0
    /// </summary>
    public sealed class KokoroTTS : TextToSpeechInference
    {
        [Serializable]
        public class Options : TextToSpeechOptions
        {
            public LanguageCode language = LanguageCode.En_US;
        }

        const int STYLE_DIM = 256;

        readonly List<OrtValue> inputs;

        long[] inputTokensBuffer;
        float[] voiceStyleBuffer;
        Memory<float> voiceStyle;
        public IG2P G2P { get; private set; }

        public float Speed { get; set; } = 1.0f;
        protected override int AudioSampleRate => 24000;

        public KokoroTTS(byte[] modelData, Options options) : base(modelData, options)
        {
            inputs = new List<OrtValue>(3);

            inputTokensBuffer = ArrayPool<long>.Shared.Rent(STYLE_DIM);
            voiceStyleBuffer = ArrayPool<float>.Shared.Rent(STYLE_DIM);

            // Set tensors
            // [input_ids] shape: 1,-1, type: System.Int64 isTensor: True
            // [style] shape: 1,256, type: System.Single isTensor: True
            // [speed] shape: 1, type: System.Single isTensor: True
            var meta = session.InputMetadata;
            inputs.Add(OrtValue.CreateTensorValueFromMemory(inputTokensBuffer, new long[] { 1, inputTokensBuffer.Length }));
            inputs.Add(meta["style"].CreateTensorOrtValue());
            inputs.Add(meta["speed"].CreateTensorOrtValue());

            G2P = new ESpeakG2P();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            G2P?.Dispose();

            foreach (var input in inputs)
            {
                input.Dispose();
            }
            if (inputTokensBuffer != null)
            {
                ArrayPool<long>.Shared.Return(inputTokensBuffer);
            }
            if (voiceStyleBuffer != null)
            {
                ArrayPool<float>.Shared.Return(voiceStyleBuffer);
            }
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

            // Convert byte[] to float[]
            var voiceStyleBytes = request.downloadHandler.data;
            int length = voiceStyleBytes.Length / sizeof(float);
            EnsureBufferSize(ref voiceStyleBuffer, length);
            Buffer.BlockCopy(voiceStyleBytes, 0, voiceStyleBuffer, 0, voiceStyleBytes.Length);
            voiceStyle = new Memory<float>(voiceStyleBuffer, 0, length);

            Debug.Log($"Voice style loaded: length:{voiceStyle.Length}");
        }

        protected override async Awaitable<IReadOnlyCollection<OrtValue>> PreProcessAsync(string input, CancellationToken cancellationToken)
        {
            await Awaitable.BackgroundThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

            // 1: Convert input text to phonemes
            string phonemes = G2P.Convert(input);
            // 2. Map phonemes to ids
            var tokens = Tokenizer.Encode(phonemes);
            PreProcess(tokens.Span);

            return inputs;
        }

        void PreProcess(ReadOnlySpan<long> tokens)
        {
            if (voiceStyle.IsEmpty)
            {
                throw new InvalidOperationException("Voice is not loaded. Call LoadVoiceAsync() first.");
            }

            Assert.IsTrue(tokens.Length < 510, $"Input too long: {tokens.Length} tokens");

            // [0]: Set input tokens
            {
                inputs[0].Dispose();
                EnsureBufferSize(ref inputTokensBuffer, tokens.Length + 2);
                var tokensSpan = inputTokensBuffer.AsSpan(0, tokens.Length + 2);
                const long PAD_ID = 0;
                tokensSpan[0] = PAD_ID; // <s>
                tokens.CopyTo(tokensSpan[1..]);
                tokensSpan[tokens.Length + 1] = PAD_ID; // </s>
                inputs[0] = OrtValue.CreateTensorValueFromMemory(inputTokensBuffer, new long[] { 1, tokensSpan.Length });
            }

            // [1]: Set voice style
            {
                int offset = tokens.Length * STYLE_DIM;
                var styleSrc = voiceStyle[offset..(offset + STYLE_DIM)].Span;
                var styleDst = inputs[1].GetTensorMutableDataAsSpan<float>();
                styleSrc.CopyTo(styleDst);
            }

            // [2]: Set speed
            {
                var speedDst = inputs[2].GetTensorMutableDataAsSpan<float>();
                speedDst[0] = Math.Clamp(Speed, 0.1f, 5f);
            }
        }

        public static char GetLanguagePrefix(LanguageCode lang)
        {
            return lang switch
            {
                LanguageCode.En_US => 'a', // American English
                LanguageCode.En_GB => 'b', // British English
                _ => throw new NotSupportedException($"Language {lang} is not supported.")
            };
        }
    }
}
