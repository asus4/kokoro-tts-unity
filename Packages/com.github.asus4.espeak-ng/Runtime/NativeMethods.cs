using System;
using System.Runtime.InteropServices;

namespace ESpeakNg
{
    public enum espeak_AUDIO_OUTPUT
    {
        AUDIO_OUTPUT_PLAYBACK = 0,
        AUDIO_OUTPUT_RETRIEVAL = 1,
        AUDIO_OUTPUT_SYNCHRONOUS = 2,
        AUDIO_OUTPUT_SYNCH_PLAYBACK = 3,
    }

    public enum espeak_ERROR
    {
        EE_OK = 0,
        EE_INTERNAL_ERROR = -1,
        EE_BUFFER_FULL = 1,
        EE_NOT_FOUND = 2
    };

    [Flags]
    public enum espeakINITIALIZE
    {
        espeakINITIALIZE_PHONEME_EVENTS = 0x0001,
        espeakINITIALIZE_PHONEME_IPA = 0x0002,
        espeakINITIALIZE_DONT_EXIT = 0x8000,
    }

    public enum espeakCHARS
    {
        espeakCHARS_AUTO = 0,
        espeakCHARS_UTF8 = 1,
        espeakCHARS_8BIT = 2,
        espeakCHARS_WCHAR = 3,
        espeakCHARS_16BIT = 4,
    }

    internal static class NativeMethods
    {
        internal class NativeLib
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            internal const string DllName = "libespeak-ng.so";
#elif UNITY_IOS && !UNITY_EDITOR
            internal const string DllName = "__Internal";
#else
            internal const string DllName = "espeak-ng";
#endif
        }

        [DllImport(NativeLib.DllName)]
        internal static extern unsafe void espeak_ng_InitializePath(byte* path);

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern unsafe int espeak_Initialize(espeak_AUDIO_OUTPUT output, int buflength, char* path, int options);

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern unsafe espeak_ERROR espeak_SetVoiceByFile(char* filename);

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern unsafe espeak_ERROR espeak_SetVoiceByName(char* name);

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern unsafe char* espeak_TextToPhonemes(void** text, int textmode, int phonememode);

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern espeak_ERROR espeak_Terminate();

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern unsafe char* espeak_Info(IntPtr* path_data);
    }
}
