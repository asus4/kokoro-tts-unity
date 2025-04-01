using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ESpeakNg
{
    public static class ESpeak
    {

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

        public unsafe static string TextToPhonemes(string text, int phonemeMode = 0)
        {
            espeakCHARS textMode = espeakCHARS.espeakCHARS_UTF8;

            // Convert C# string to UTF-8 with null-terminator
            byte[] textUtf8 = Encoding.UTF8.GetBytes(text);
            byte[] utf8TextWithTerminator = new byte[textUtf8.Length + 1];
            textUtf8.AsSpan().CopyTo(utf8TextWithTerminator);
            utf8TextWithTerminator[textUtf8.Length] = 0; // Null-terminate the string

            fixed (void* utf8textPtr = utf8TextWithTerminator)
            {
                IntPtr phonemesPtr = (IntPtr)NativeMethods.espeak_TextToPhonemes(&utf8textPtr, (int)textMode, phonemeMode);
                if (phonemesPtr == IntPtr.Zero)
                {
                    return string.Empty;
                }
                return Marshal.PtrToStringAuto(phonemesPtr);
            }
        }

        public static espeak_ERROR Terminate()
        {
            return NativeMethods.espeak_Terminate();
        }
    }
}
