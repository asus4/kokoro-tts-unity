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


    [Flags]
    public enum espeakPhonemesOptions
    {
        espeakPHONEMES_SHOW = 0x01,
        espeakPHONEMES_IPA = 0x02,
        espeakPHONEMES_TRACE = 0x08,
        espeakPHONEMES_MBROLA = 0x10,
        espeakPHONEMES_TIE = 0x80,
    }

    public enum espeak_ng_STATUS
    {
        ENS_GROUP_MASK = 0x70000000,
        ENS_GROUP_ERRNO = 0x00000000, // Values 0-255 map to errno error codes.
        ENS_GROUP_ESPEAK_NG = 0x10000000, // eSpeak NG error codes.

        // eSpeak NG 1.49.0
        ENS_OK = 0,
        ENS_COMPILE_ERROR = 0x100001FF,
        ENS_VERSION_MISMATCH = 0x100002FF,
        ENS_FIFO_BUFFER_FULL = 0x100003FF,
        ENS_NOT_INITIALIZED = 0x100004FF,
        ENS_AUDIO_ERROR = 0x100005FF,
        ENS_VOICE_NOT_FOUND = 0x100006FF,
        ENS_MBROLA_NOT_FOUND = 0x100007FF,
        ENS_MBROLA_VOICE_NOT_FOUND = 0x100008FF,
        ENS_EVENT_BUFFER_FULL = 0x100009FF,
        ENS_NOT_SUPPORTED = 0x10000AFF,
        ENS_UNSUPPORTED_PHON_FORMAT = 0x10000BFF,
        ENS_NO_SPECT_FRAMES = 0x10000CFF,
        ENS_EMPTY_PHONEME_MANIFEST = 0x10000DFF,
        ENS_SPEECH_STOPPED = 0x10000EFF,

        // eSpeak NG 1.49.2
        ENS_UNKNOWN_PHONEME_FEATURE = 0x10000FFF,
        ENS_UNKNOWN_TEXT_ENCODING = 0x100010FF,
    }

    public enum espeak_ng_OUTPUT_MODE : uint
    {
        ENOUTPUT_MODE_SYNCHRONOUS = 0x0001,
        ENOUTPUT_MODE_SPEAK_AUDIO = 0x0002,
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

        #region Methods in speak_lib.h
        [DllImport(NativeLib.DllName)]
        internal static extern unsafe int espeak_Initialize(espeak_AUDIO_OUTPUT output, int buflength, byte* path, int options);

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern unsafe espeak_ERROR espeak_SetVoiceByFile(char* filename);

        [DllImport(NativeLib.DllName)]
        internal static extern unsafe espeak_ERROR espeak_SetVoiceByName(byte* name);

        [DllImport(NativeLib.DllName)]
        internal static extern espeak_ERROR espeak_SetVoiceByProperties(IntPtr /* espeak_VOICE */ voice_spec);

        [DllImport(NativeLib.DllName)]
        internal static extern unsafe void* espeak_TextToPhonemes(void** utf8Text, int textMode, int phonemeMode);

        [DllImport(NativeLib.DllName)]
        internal static extern unsafe void* espeak_TextToPhonemesWithTerminator(void** textPtr, int textMode, int phonemeMode, int* terminator);

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern espeak_ERROR espeak_Terminate();

        [DllImport(NativeLib.DllName, CharSet = CharSet.Ansi)]
        internal static extern unsafe char* espeak_Info(IntPtr* path_data);

        #endregion // Methods in speak_lib.ho

        #region Methods in espeak_ng.h

        [DllImport(NativeLib.DllName)]
        internal static extern unsafe void espeak_ng_InitializePath(byte* path);

        [DllImport(NativeLib.DllName)]
        internal static extern unsafe espeak_ng_STATUS espeak_ng_InitializeOutput(espeak_ng_OUTPUT_MODE output_mode, int buffer_length, byte* device);
        #endregion // Methods in espeak_ng.h
    }
}
