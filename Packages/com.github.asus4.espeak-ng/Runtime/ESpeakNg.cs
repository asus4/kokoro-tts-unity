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

            byte[] pathBytes = Encoding.UTF8.GetBytes(path);
            fixed (byte* pathPtr = pathBytes)
            {
                NativeMethods.espeak_ng_InitializePath(pathPtr);
            }
        }

        public unsafe static int Initialize(string path, espeakINITIALIZE options)
        {
            fixed (char* pathPtr = path)
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

        public unsafe static espeak_ERROR SetVoiceByFile(string filename)
        {
            fixed (char* filenamePtr = filename)
            {
                return NativeMethods.espeak_SetVoiceByFile(filenamePtr);
            }
        }

        public unsafe static espeak_ERROR SetVoiceByName(string name)
        {
            fixed (char* namePtr = name)
            {
                return NativeMethods.espeak_SetVoiceByName(namePtr);
            }
        }

        public unsafe static string TextToPhonemes(string text, int phonememode = 0)
        {
            // Encode UTF-16 to UTF-8
            espeakCHARS textmode = espeakCHARS.espeakCHARS_UTF8;
            byte[] utf8text = Encoding.UTF8.GetBytes(text);


            fixed (byte* utf8textPtr = utf8text)
            {
                void* textPtr = (void*)utf8textPtr;
                IntPtr phonemesPtr = (IntPtr)NativeMethods.espeak_TextToPhonemes(&textPtr, (int)textmode, phonememode);
                if (phonemesPtr == IntPtr.Zero)
                {
                    return null;
                }
                return Marshal.PtrToStringAnsi(phonemesPtr);
            }
        }

        public static espeak_ERROR Terminate()
        {
            return NativeMethods.espeak_Terminate();
        }
    }
}
