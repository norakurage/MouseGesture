using System;
using System.Text;

namespace WindowsFormsApp1
{
    public static class ProcessUtils
    {
        public static string GetActiveProcessPath()
        {
            IntPtr hWnd = NativeMethods.GetForegroundWindow();
            return GetProcessPathFromWindowHandle(hWnd);
        }

        public static string GetProcessPathFromWindowHandle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
            return GetProcessPath(processId);
        }

        public static string GetProcessPath(uint processId)
        {
            try
            {
                IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        StringBuilder buffer = new StringBuilder(1024);
                        uint size = (uint)buffer.Capacity;
                        if (NativeMethods.QueryFullProcessImageName(hProcess, 0, buffer, ref size))
                        {
                            return buffer.ToString();
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(hProcess);
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }
    }
}
