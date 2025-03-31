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
[RequireComponent(typeof(UIDocument), typeof(AudioSource))]
sealed class KokoroTTSDemo : MonoBehaviour
{
    [SerializeField]
    RemoteFile modelUrl = new("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/onnx/model_fp16.onnx");

    [SerializeField]
    KokoroTTS.Options options;

    [SerializeField]
    [ContextMenuItem("Update Voice List", nameof(UpdateVoiceList))]
    string[] allVoices;

    [SerializeField]
    string speechText = "Life is like a box of chocolates. You never know what you're gonna get.";

    [SerializeField]
    [Range(0.1f, 5f)]
    float speechSpeed = 1f;

    KokoroTTS tts;
    AudioSource audioSource;

    async void Start()
    {
        Application.runInBackground = true;
        audioSource = GetComponent<AudioSource>();

        // Setup Kokoro
        Debug.Log("Loading model...");
        CancellationToken cancellationToken = destroyCancellationToken;
        byte[] modelData = await modelUrl.Load(cancellationToken);
        await Awaitable.MainThreadAsync();
        cancellationToken.ThrowIfCancellationRequested();

        tts = new KokoroTTS(modelData, options);
        Debug.Log("TTS created");

        // Setup UI
        var uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        // Filter voice list to only supported language
        var voicesDropdown = root.Q<DropdownField>("VoicesDropdown");
        char selectedLangPrefix = KokoroTTS.GetLanguagePrefix(options.language);
        voicesDropdown.choices = allVoices
            .Where(voice => voice[0] == selectedLangPrefix)
            .ToList();
        voicesDropdown.RegisterValueChangedCallback(async evt =>
        {
            int index = voicesDropdown.index;
            await LoadVoiceAsync(index);
            Debug.Log($"Selected voice: {allVoices[index]}");
        });
        voicesDropdown.index = 0;

        var ttsTextField = root.Q<TextField>("TtsTextField");
        ttsTextField.value = speechText;
        ttsTextField.RegisterValueChangedCallback(evt
            => speechText = evt.newValue);

        var speechButton = root.Q<Button>("SpeechButton");
        speechButton.RegisterCallback<ClickEvent>(async evt
            => await GenerateAsync());
    }

    void OnDestroy()
    {
        tts?.Dispose();
    }

    async Awaitable LoadVoiceAsync(int index)
    {
        string url = "file://" + Path.Combine(Application.streamingAssetsPath, "Voices", $"{allVoices[index]}.bin");
        await tts.LoadVoiceAsync(new Uri(url), destroyCancellationToken);
    }

    async Awaitable GenerateAsync()
    {
        Debug.Log($"Generating speech: {speechText}");
        tts.Speed = speechSpeed;
        var clip = await tts.GenerateAudioClipAsync(speechText, destroyCancellationToken);
        Debug.Log("Speech generated");

        if (audioSource.clip != null)
        {
            Destroy(audioSource.clip);
        }
        audioSource.clip = clip;
        audioSource.Play();
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
            allVoices = Array.Empty<string>();
            return;
        }
        allVoices = Directory.GetFiles(dir, "*.bin")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToArray();
    }
}
