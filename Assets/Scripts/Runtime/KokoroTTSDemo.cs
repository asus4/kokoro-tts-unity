using System;
using System.IO;
using System.Linq;
using System.Threading;
using Kokoro;
using Microsoft.ML.OnnxRuntime.Examples;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Basic usage of KokoroTTS.
/// 
/// Port of
/// https://github.com/hexgrad/kokoro
/// Licensed under Apache License 2.0
/// </summary>
[RequireComponent(typeof(UIDocument))]
sealed class KokoroTTSDemo : MonoBehaviour
{
    [SerializeField]
    RemoteFile modelUrl = new("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/onnx/model_fp16.onnx");

    [SerializeField]
    KokoroTTS.Options options;

    [SerializeField]
    [ContextMenuItem("Update Voice List", nameof(UpdateVoiceList))]
    string[] voiceList;

    [SerializeField]
    string speechText = "Life is like a box of chocolates. You never know what you're gonna get.";

    KokoroTTS tts;

    async void Start()
    {
        // Setup Kokoro
        Debug.Log("Loading model...");
        CancellationToken cancellationToken = destroyCancellationToken;
        byte[] modelData = await modelUrl.Load(cancellationToken);
        tts = await KokoroTTS.CreateAsync(modelData, options, cancellationToken);
        Debug.Log("TTS created");

        // Setup UI
        var uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        var voicesDropdown = root.Q<DropdownField>("VoicesDropdown");
        voicesDropdown.choices = voiceList.ToList();
        voicesDropdown.RegisterValueChangedCallback(async evt =>
        {
            int index = voicesDropdown.index;
            await LoadVoiceAsync(index);
            Debug.Log($"Selected voice: {voiceList[index]}");
        });
        voicesDropdown.index = 0;

        var ttsTextField = root.Q<TextField>("TtsTextField");
        ttsTextField.value = speechText;
        ttsTextField.RegisterValueChangedCallback(evt
            => speechText = evt.newValue);

        var speechButton = root.Q<Button>("SpeechButton");
        speechButton.RegisterCallback<ClickEvent>(async evt
            => await tts.RunAsync(speechText, cancellationToken));
    }

    void OnDestroy()
    {
        tts?.Dispose();
    }

    async Awaitable LoadVoiceAsync(int index)
    {
        string url = "file://" + Path.Combine(Application.streamingAssetsPath, "Voices", $"{voiceList[index]}.bin");
        await tts.LoadVoiceAsync(new Uri(url), destroyCancellationToken);
    }

    /// <summary>
    /// Voice list is set from the inspector.
    /// </summary>
    [ContextMenu("Update Voice List")]
    void UpdateVoiceList()
    {
        string dir = Path.Combine(Application.streamingAssetsPath, "Voices");
        if (!Directory.Exists(dir))
        {
            Debug.LogWarning($"Voice directory does not exist: {dir}");
            voiceList = Array.Empty<string>();
            return;
        }
        voiceList = Directory.GetFiles(dir, "*.bin")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToArray();
    }
}
