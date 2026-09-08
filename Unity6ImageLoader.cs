using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace DetailedMaps
{
    /// <summary>
    /// Unity 6 ImageConversion.LoadImage via ICall.
    /// MelonLoader 0.7.2's Il2CppImageConversionManager still uses pre-Unity-6 signatures.
    /// </summary>
    internal static class Unity6ImageLoader
    {
        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct ManagedSpanWrapper
        {
            public void* begin;
            public int length;
        }

        private delegate bool LoadImageDelegate_Unity6(IntPtr tex, ref ManagedSpanWrapper data, bool markNonReadable);
        private static readonly LoadImageDelegate_Unity6? LoadImage_Unity6;

        static Unity6ImageLoader()
        {
            LoadImage_Unity6 = IL2CPP.ResolveICall<LoadImageDelegate_Unity6>(
                "UnityEngine.ImageConversion::LoadImage_Injected");
        }

        public static Texture2D LoadPng(byte[] pngBytes, string name)
        {
            if (pngBytes == null || pngBytes.Length == 0)
            {
                throw new ArgumentException("PNG data is empty.", nameof(pngBytes));
            }

            if (LoadImage_Unity6 == null)
            {
                throw new NullReferenceException("UnityEngine.ImageConversion::LoadImage_Injected was not resolved.");
            }

            // Keep a strong Il2Cpp array reference for the duration of the native call.
            Il2CppStructArray<byte> data = pngBytes;
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            unsafe
            {
                IntPtr arrayPtr = IL2CPP.Il2CppObjectBaseToPtrNotNull(data);
                ManagedSpanWrapper span = new ManagedSpanWrapper
                {
                    begin = (void*)(arrayPtr + 0x20),
                    length = data.Length
                };

                bool ok = LoadImage_Unity6(tex.m_CachedPtr, ref span, markNonReadable: false);
                GC.KeepAlive(data);
                GC.KeepAlive(tex);

                if (!ok)
                {
                    UnityEngine.Object.Destroy(tex);
                    throw new Exception($"LoadImage_Injected failed for '{name}' ({pngBytes.Length} bytes).");
                }
            }

            return tex;
        }
    }
}
