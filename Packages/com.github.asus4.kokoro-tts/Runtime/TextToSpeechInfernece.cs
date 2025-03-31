#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;

namespace Microsoft.ML.OnnxRuntime.Unity
{
    [Serializable]

    public class TextToSpeechOptions
    {
        public ExecutionProviderOptions executionProvider = new();
    }

    public abstract class TextToSpeechInference : IDisposable
    {
        protected InferenceSession session;
        protected SessionOptions sessionOptions;
        protected RunOptions runOptions;
        float[] outputAudioBuffer;

        bool disposed;

        protected abstract int AudioSampleRate { get; }

        public TextToSpeechInference(byte[] modelData, TextToSpeechOptions options)
        {
            try
            {
                sessionOptions = new SessionOptions();
                options.executionProvider.AppendExecutionProviders(sessionOptions);
                session = new InferenceSession(modelData, sessionOptions);
                runOptions = new RunOptions();
            }
            catch (Exception e)
            {
                session?.Dispose();
                sessionOptions?.Dispose();
                runOptions?.Dispose();
                throw e;
            }
            session.LogIOInfo();

            outputAudioBuffer = ArrayPool<float>.Shared.Rent(AudioSampleRate * 10);
        }

        ~TextToSpeechInference()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }
            if (disposing)
            {
                session?.Dispose();
                sessionOptions?.Dispose();
                runOptions?.Dispose();
                if (outputAudioBuffer != null)
                {
                    ArrayPool<float>.Shared.Return(outputAudioBuffer);
                }
            }
            disposed = true;
        }

        public virtual async Awaitable<ReadOnlyMemory<float>> GenerateAsync(string input, CancellationToken cancellationToken)
        {
            await Awaitable.BackgroundThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

            // Pre process input
            var inputs = await PreProcessAsync(input, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Run inference
            using var disposableOutputs = session.Run(runOptions, session.InputNames, inputs, session.OutputNames);
            cancellationToken.ThrowIfCancellationRequested();

            // Post process output
            var result = PostProcess(disposableOutputs);
            await Awaitable.MainThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }

        public virtual async Awaitable<AudioClip> GenerateAudioClipAsync(string input, CancellationToken cancellationToken)
        {
            var result = await GenerateAsync(input, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Convert to AudioClip
            var clip = AudioClip.Create("TTS", result.Length, 1, AudioSampleRate, false);
            clip.SetData(result.Span, 0);
            return clip;
        }

        protected abstract Awaitable<IReadOnlyCollection<OrtValue>> PreProcessAsync(string input, CancellationToken cancellationToken);

        protected virtual ReadOnlyMemory<float> PostProcess(IReadOnlyList<OrtValue> outputs)
        {
            Assert.IsTrue(outputs.Count == 1, $"Expected 1 output, but got {outputs.Count}");

            var tmpBuffer = outputs[0].GetTensorDataAsSpan<float>();
            int length = tmpBuffer.Length;

            EnsureBufferSize(ref outputAudioBuffer, length);
            var memory = new Memory<float>(outputAudioBuffer, 0, length);
            tmpBuffer.CopyTo(memory.Span);

            return memory;
        }

        static protected void EnsureBufferSize<T>(ref T[] buffer, int length)
        {
            Assert.IsNotNull(buffer);
            if (buffer.Length < length)
            {
                ArrayPool<T>.Shared.Return(buffer);
                buffer = ArrayPool<T>.Shared.Rent(length);
            }
        }
    }
}
