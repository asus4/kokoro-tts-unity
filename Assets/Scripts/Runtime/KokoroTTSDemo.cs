using Microsoft.ML.OnnxRuntime.Examples;
using UnityEngine;
using Kokoro;

/// <summary>
/// Basic usage of KokoroTTS.
/// </summary>
sealed class KokoroTTSDemo : MonoBehaviour
{
    [SerializeField]
    RemoteFile modelUrl = new("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/onnx/model_fp16.onnx");

    [SerializeField]
    KokoroTTS.Options options;

    KokoroTTS tts;

    async void Start()
    {
        Debug.Log("Loading model...");
        byte[] modelData = await modelUrl.Load(default);
        Debug.Log("Model loaded");
        tts = new KokoroTTS(modelData, options);
    }

    void OnDestroy()
    {
        tts?.Dispose();
    }
}
