using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace DetailedMaps
{
    /// <summary>
    /// MelonLoader 0.7.2 stock LoadFromMemory can throw ObjectCollectedException (Unity 6 Span path).
    /// Uses LoadFromMemory_Internal_Injected (Unity 6 ICall).
    /// </summary>
    internal static class Unity6AssetBundleLoader
    {
        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct ManagedSpanWrapper
        {
            public void* begin;
            public int length;
        }

        private delegate IntPtr LoadFromMemory_Internal_Unity6(ref ManagedSpanWrapper binary, uint crc);

        private static readonly LoadFromMemory_Internal_Unity6? LoadFromMemoryInjected;

        static Unity6AssetBundleLoader()
        {
            LoadFromMemoryInjected = IL2CPP.ResolveICall<LoadFromMemory_Internal_Unity6>(
                "UnityEngine.AssetBundle::LoadFromMemory_Internal_Injected");
        }

        public static AssetBundle? LoadFromMemory(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("AssetBundle data is empty.", nameof(bytes));
            }

            if (LoadFromMemoryInjected == null)
            {
                throw new NullReferenceException(
                    "UnityEngine.AssetBundle::LoadFromMemory_Internal_Injected was not resolved.");
            }

            Il2CppStructArray<byte> data = bytes;

            unsafe
            {
                IntPtr arrayPtr = IL2CPP.Il2CppObjectBaseToPtrNotNull(data);
                ManagedSpanWrapper span = new ManagedSpanWrapper
                {
                    begin = (void*)(arrayPtr + 0x20),
                    length = data.Length
                };

                IntPtr raw = LoadFromMemoryInjected(ref span, 0u);
                GC.KeepAlive(data);

                if (raw == IntPtr.Zero)
                {
                    return null;
                }

                IntPtr viaGcTarget = IL2CPP.il2cpp_gchandle_get_target(raw);
                if (viaGcTarget != IntPtr.Zero)
                {
                    return new AssetBundle(viaGcTarget);
                }

                try
                {
                    IntPtr viaReadPtr = Marshal.ReadIntPtr(raw);
                    if (viaReadPtr != IntPtr.Zero)
                    {
                        return new AssetBundle(viaReadPtr);
                    }
                }
                catch
                {
                    // ignore
                }

                return new AssetBundle(raw);
            }
        }
    }
}
