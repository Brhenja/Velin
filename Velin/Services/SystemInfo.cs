using System;
using System.Runtime.InteropServices;

namespace Velin.Services
{
    public readonly record struct MemoryInfo(long TotalMB, long FreeMB);

    // Datos de la PC que usamos para avisar si le asignás a Minecraft más RAM de la conveniente.
    public static class SystemInfo
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public static MemoryInfo? GetMemory()
        {
            try
            {
                var status = new MEMORYSTATUSEX();
                if (!GlobalMemoryStatusEx(status)) return null;
                return new MemoryInfo((long)(status.ullTotalPhys / 1048576UL), (long)(status.ullAvailPhys / 1048576UL));
            }
            catch { return null; }
        }

        // Recomendado: hasta la mitad de la RAM de la PC (mínimo 1024 MB), en múltiplos de 512.
        public static int RecommendedMaxRamMB(MemoryInfo memory)
        {
            var half = (int)Math.Min(int.MaxValue, memory.TotalMB / 2);
            return Math.Max(1024, half / 512 * 512);
        }
    }
}
