using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace Velin.Services
{
    // Devuelve a Windows la memoria que el launcher no está usando. Se llama cuando la ventana se minimiza o se
    // esconde mientras se juega: ahí el launcher no hace nada y esa RAM le sirve más al juego. La memoria que se
    // necesite después se vuelve a pedir sola (la primera vez que se abre la ventana tarda un instante más).
    public static class MemoryTrimmer
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

        public static void Trim()
        {
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                if (OperatingSystem.IsWindows())
                {
                    using var self = Process.GetCurrentProcess();
                    SetProcessWorkingSetSize(self.Handle, (IntPtr)(-1), (IntPtr)(-1)); // -1/-1 = "sacá de la RAM todo lo que puedas"
                }
            }
            catch { /* es sólo una optimización: si falla, no pasa nada */ }
        }
    }
}
