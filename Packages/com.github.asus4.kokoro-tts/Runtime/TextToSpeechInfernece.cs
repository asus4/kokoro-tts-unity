using System;
using System.Threading;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Unity;
using Unity.Profiling;
using UnityEngine;

namespace Microsoft.ML.OnnxRuntime.Unity
{
    [Serializable]

    public class TextToSpeechOptions
    {
        public ExecutionProviderOptions executionProvider;
    }

    public abstract class TextToSpeechInference : IDisposable
    {
        protected readonly InferenceSession session;
        protected readonly SessionOptions sessionOptions;
        protected readonly RunOptions runOptions;
        bool disposed;

        public TextToSpeechInference(byte[] modelData, TextToSpeechOptions options)
        {
            Debug.Log($"model: {modelData.Length} bytes, options: {options.executionProvider}");

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
            }
            disposed = true;
        }

        public virtual async Awaitable<ReadOnlyMemory<byte>> RunAsync(string input, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("RunAsync is not implemented");
        }
    }
}
