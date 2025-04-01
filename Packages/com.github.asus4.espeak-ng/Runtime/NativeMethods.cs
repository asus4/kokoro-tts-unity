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
        espeakPHONEMES_TIE = 0x0080,
        espeakINITIALIZE_DONT_EXIT = 0x8000,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct espeak_VOICE
    {
        /// <summary>A given name for this voice. UTF8 string.</summary>
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string name;

        /// <summary>List of pairs of (byte) priority + (string) language (and dialect qualifier)</summary>
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string languages;

        /// <summary>The filename for this voice within espeak-ng-data/voices</summary>
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string identifier;

        /// <summary>0=none 1=male, 2=female</summary>
        public byte gender;

        /// <summary>0=not specified, or age in years</summary>
        public byte age;

        /// <summary>Only used when passed as a parameter to espeak_SetVoiceByProperties</summary>
        public byte variant;
        /// <summary>For internal use</summary>
        public byte xx1;
        /// <summary>For internal use</summary>
        public int score;
        /// <summary>For internal use</summary>
        public IntPtr spare;
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

        [DllImport(NativeLib.DllName)]
        internal static extern unsafe int espeak_Initialize(espeak_AUDIO_OUTPUT output, int buflength, byte* path, int options);

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern unsafe espeak_ERROR espeak_SetVoiceByFile(char* filename);

        [DllImport(NativeLib.DllName)]
        internal static extern unsafe espeak_ERROR espeak_SetVoiceByName(byte* name);

        [DllImport(NativeLib.DllName)]
        internal static extern espeak_ERROR espeak_SetVoiceByProperties(IntPtr /* espeak_VOICE */ voice_spec);

        [DllImport(NativeLib.DllName)]
        internal static extern unsafe void* espeak_TextToPhonemes(void** text, int textmode, int phonememode);

        [DllImport(NativeLib.DllName)]
        internal static extern unsafe void* espeak_TextToPhonemesWithTerminator(void** textptr, int textmode, int phonememode, int* terminator);

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern espeak_ERROR espeak_Terminate();

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern unsafe char* espeak_Info(IntPtr* path_data);
    }
}
