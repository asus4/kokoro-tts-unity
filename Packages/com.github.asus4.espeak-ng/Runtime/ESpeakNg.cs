using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ESpeakNg
{
    public static class ESpeak
    {

        public unsafe static void InitializePath(string path)
        {
            fixed (char* pathPtr = path)
            {
                NativeMethods.espeak_ng_InitializePath((IntPtr)pathPtr);
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

        public unsafe static string GetInfo(out string path)
        {
            IntPtr pathPtr;
            IntPtr versionPrt = (IntPtr)NativeMethods.espeak_Info(&pathPtr);
            if (versionPrt == IntPtr.Zero || pathPtr == IntPtr.Zero)
            {
                path = null;
                return null;
            }
            path = Marshal.PtrToStringAnsi(pathPtr);
            return Marshal.PtrToStringAnsi(versionPrt);
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
