using System;
using System.Collections.Generic;
using System.Text;

namespace WindowsFormsApp1
{
    public static class ProcessUtils
    {
        private static readonly Dictionary<uint, (string Path, DateTime ExpireTime)> _processCache = new Dictionary<uint, (string, DateTime)>();
        private static readonly object _cacheLock = new object();

        public static IntPtr GetForegroundWindowHandle()
        {
            return NativeMethods.GetForegroundWindow();
        }

        public static string GetActiveProcessPath()
        {
            return GetProcessPathFromWindowHandle(GetForegroundWindowHandle());
        }

        public static string GetProcessPathFromWindowHandle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
            return GetProcessPath(processId);
        }

        public static string GetProcessPath(uint processId)
        {
            DateTime now = DateTime.UtcNow;

            lock (_cacheLock)
            {
                if (_processCache.TryGetValue(processId, out var entry))
                {
                    if (now < entry.ExpireTime)
                    {
                        return entry.Path;
                    }
                    else
                    {
                        _processCache.Remove(processId);
                    }
                }
            }

            string path = null;

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
                            path = buffer.ToString();
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

            if (path != null)
            {
                lock (_cacheLock)
                {
                    if (_processCache.Count > 100)
                    {
                        var keysToRemove = new List<uint>();
                        foreach (var kvp in _processCache)
                        {
                            if (now >= kvp.Value.ExpireTime)
                            {
                                keysToRemove.Add(kvp.Key);
                            }
                        }
                        foreach (var key in keysToRemove)
                        {
                            _processCache.Remove(key);
                        }
                        
                        if (_processCache.Count > 200)
                        {
                            _processCache.Clear(); // Fallback to avoid infinite growth
                        }
                    }

                    // キャッシュの有効期限を設定 (例: 10分)
                    _processCache[processId] = (path, now.AddMinutes(10));
                }
            }

            return path;
        }
    }
}
