using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ESpeakNg
{
    public static class ESpeak
    {
        #region Methods in speak_lib.h

        public unsafe static int Initialize(string path, espeakINITIALIZE options)
        {
            byte[] pathUtf8 = Encoding.UTF8.GetBytes(path);
            fixed (byte* pathPtr = pathUtf8)
            {
                return NativeMethods.espeak_Initialize(
                    espeak_AUDIO_OUTPUT.AUDIO_OUTPUT_RETRIEVAL,
                    0,
                    pathPtr,
                    (int)options);
            }
        }

        public unsafe static (string version, string dataPath) GetInfo()
        {
            IntPtr dataPathPtr;
            IntPtr versionPtr = (IntPtr)NativeMethods.espeak_Info(&dataPathPtr);
            if (versionPtr == IntPtr.Zero || dataPathPtr == IntPtr.Zero)
            {
                return (string.Empty, string.Empty);
            }
            string dataPath = Marshal.PtrToStringAuto(dataPathPtr);
            string version = Marshal.PtrToStringAuto(versionPtr);
            return (version, dataPath);
        }

        public static espeak_ERROR SetVoiceByProperties(espeak_VOICE voice)
        {
            IntPtr voicePtr = Marshal.AllocHGlobal(Marshal.SizeOf(voice));
            try
            {
                Marshal.StructureToPtr(voice, voicePtr, false);
                return NativeMethods.espeak_SetVoiceByProperties(voicePtr);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to set voice properties: {e.Message}");
                return espeak_ERROR.EE_INTERNAL_ERROR;
            }
            finally
            {
                Marshal.FreeHGlobal(voicePtr);
            }
        }

        public static espeak_ERROR SetLanguage(string language)
        {
            var voice = new espeak_VOICE()
            {
                name = null,
                languages = language,
                identifier = null,
                gender = 0,
                age = 0,
                variant = 0,
                xx1 = 0,
                score = 0,
                spare = IntPtr.Zero
            };
            return SetVoiceByProperties(voice);
        }

        public unsafe static espeak_ERROR SetVoiceByFile(string filename)
        {
            fixed (char* filenamePtr = filename)
            {
                return NativeMethods.espeak_SetVoiceByFile(filenamePtr);
            }
        }

        public unsafe static espeak_ERROR SetVoiceByName(string name)
        {
            byte[] nameUtf8 = Encoding.UTF8.GetBytes(name);
            fixed (byte* namePtr = nameUtf8)
            {
                return NativeMethods.espeak_SetVoiceByName(namePtr);
            }
        }

        public const espeakPhonemesOptions DefaultPhonemeOptions = espeakPhonemesOptions.espeakPHONEMES_IPA | espeakPhonemesOptions.espeakPHONEMES_TIE;
        /// <summary>U+200D ZERO WIDTH JOINER</summary>
        public const int DefaultPhonemesSeparator = 0x200d;

        public unsafe static IReadOnlyList<string> TextToPhonemes(
            ReadOnlySpan<char> text,
            espeakPhonemesOptions phonemeOptions = DefaultPhonemeOptions,
            int phonemesSeparator = DefaultPhonemesSeparator)
        {
            const int textMode = (int)espeakCHARS.espeakCHARS_UTF8;

            // Convert C# string to UTF-8 with null-terminator (0)
            int requiredBytes = Encoding.UTF8.GetByteCount(text);
            byte[] utf8Buffer = ArrayPool<byte>.Shared.Rent(requiredBytes + 1);
            utf8Buffer.AsSpan().Fill(0);
            Encoding.UTF8.GetBytes(text, utf8Buffer.AsSpan(0, requiredBytes));

            int loopCount = 0;
            var results = new List<string>();

            /*
            phoneme_mode
            bit 1:   0=eSpeak's ascii phoneme names, 1= International Phonetic Alphabet (as UTF-8 characters).
            bit 7:   use (bits 8-23) as a tie within multi-letter phonemes names
            bits 8-23:  separator character, between phoneme names
            */
            int phonemeMode = ((int)phonemeOptions) | (phonemesSeparator << 8);

            fixed (void* utf8BufferPtr = utf8Buffer)
            {
                while (true)
                {
                    IntPtr phonemesPtr = (IntPtr)NativeMethods.espeak_TextToPhonemes(
                        &utf8BufferPtr,
                        textMode, phonemeMode);
                    if (phonemesPtr == IntPtr.Zero)
                    {
                        break;
                    }
                    string phonemes = Marshal.PtrToStringUTF8(phonemesPtr);
                    if (string.IsNullOrEmpty(phonemes))
                    {
                        break;
                    }
                    results.Add(phonemes);

                    // Fail-safe check to prevent infinite loop
                    if (loopCount++ > text.Length)
                    {
                        throw new Exception($"TextToPhonemes loop count exceeded: {loopCount}");
                    }
                }
            }

            ArrayPool<byte>.Shared.Return(utf8Buffer);
            return results;
        }

        public static espeak_ERROR Terminate()
        {
            return NativeMethods.espeak_Terminate();
        }

        #endregion // Methods in speak_lib.ho

        #region Methods in espeak_ng.h

        public unsafe static void InitializePath(string path)
        {
            if (path.Length > 160)
            {
                throw new ArgumentException($"Path length exceeds 160 characters: {path}");
            }

            byte[] pathUtf8 = Encoding.UTF8.GetBytes(path);
            fixed (byte* pathPtr = pathUtf8)
            {
                NativeMethods.espeak_ng_InitializePath(pathPtr);
            }
        }

        public unsafe static espeak_ng_STATUS InitializeOutput(espeak_ng_OUTPUT_MODE outputMode, int bufferLength, string device)
        {
            if (string.IsNullOrWhiteSpace(device))
            {
                return NativeMethods.espeak_ng_InitializeOutput(outputMode, 0, null);
            }

            byte[] deviceUtf8 = Encoding.UTF8.GetBytes(device);
            fixed (byte* devicePtr = deviceUtf8)
            {
                return NativeMethods.espeak_ng_InitializeOutput(outputMode, bufferLength, devicePtr);
            }
        }

        #endregion // Methods in espeak_ng.h
    }
}
