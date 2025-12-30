using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowsFormsApp1
{
    public class MouseHookEventArgs : EventArgs
    {
        public MousePoint Point { get; }
        public int MouseData { get; }
        public bool Handled { get; set; }

        public MouseHookEventArgs(MousePoint point, int mouseData = 0)
        {
            Point = point;
            MouseData = mouseData;
            Handled = false;
        }
    }

    public sealed class MouseHook : IDisposable
    {
        public delegate void MouseEventHandler(object sender, MouseHookEventArgs e);
        public event MouseEventHandler LeftButtonDown;
        public event MouseEventHandler LeftButtonUp;
        public event MouseEventHandler RightButtonDown;
        public event MouseEventHandler RightButtonUp;
        public event MouseEventHandler MiddleButtonDown;
        public event MouseEventHandler MiddleButtonUp;
        public event MouseEventHandler XButtonDown;
        public event MouseEventHandler XButtonUp;
        public event MouseEventHandler MouseMove;
        public event MouseEventHandler MouseWheel;

        private readonly LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private bool _disposed;

        // 他プロセスとの衝突確率を下げる（任意）
        public static readonly IntPtr INJECTED_EVENT_ID =
            new IntPtr(unchecked((int)0x12345000) ^ Process.GetCurrentProcess().Id);

        public MouseHook()
        {
            _proc = HookCallback; // delegate保持（GC防止）
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MouseHook));
            if (_hookID != IntPtr.Zero) return; // 既に開始済み

            _hookID = SetHook(_proc);
            if (_hookID == IntPtr.Zero)
            {
                // SetWindowsHookEx失敗時
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, "SetWindowsHookEx(WH_MOUSE_LL) に失敗しました。");
            }
        }

        public void Stop()
        {
            if (_hookID == IntPtr.Zero) return;

            try
            {
                UnhookWindowsHookEx(_hookID);
            }
            finally
            {
                _hookID = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            GC.SuppressFinalize(this);
        }

        ~MouseHook()
        {
            // 万一Disposeされなくても解除できるように（保険）
            try { Stop(); } catch { }
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(
                    WH_MOUSE_LL,
                    proc,
                    GetModuleHandle(curModule.ModuleName),
                    0
                );
            }
        }

        // Structures for SendInput
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        public const int INPUT_MOUSE = 0;
        public const int INPUT_KEYBOARD = 1;
        public const int INPUT_HARDWARE = 2;

        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return CallNextHookEx(_hookID, nCode, wParam, lParam);

            try
            {
                var hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                // Ignore events injected by this application
                if (hookStruct.dwExtraInfo == INJECTED_EVENT_ID)
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);

                var p = new MousePoint(hookStruct.pt.x, hookStruct.pt.y);
                var e = new MouseHookEventArgs(p, (int)hookStruct.mouseData);

                var msg = (MouseMessages)wParam;

                // 例外が漏れると入力系が不安定になるのでガード
                try
                {
                    switch (msg)
                    {
                        case MouseMessages.WM_LBUTTONDOWN: LeftButtonDown?.Invoke(this, e); break;
                        case MouseMessages.WM_LBUTTONUP: LeftButtonUp?.Invoke(this, e); break;
                        case MouseMessages.WM_RBUTTONDOWN: RightButtonDown?.Invoke(this, e); break;
                        case MouseMessages.WM_RBUTTONUP: RightButtonUp?.Invoke(this, e); break;
                        case MouseMessages.WM_MBUTTONDOWN: MiddleButtonDown?.Invoke(this, e); break;
                        case MouseMessages.WM_MBUTTONUP: MiddleButtonUp?.Invoke(this, e); break;
                        case MouseMessages.WM_XBUTTONDOWN: XButtonDown?.Invoke(this, e); break;
                        case MouseMessages.WM_XBUTTONUP: XButtonUp?.Invoke(this, e); break;
                        case MouseMessages.WM_MOUSEMOVE: MouseMove?.Invoke(this, e); break;
                        case MouseMessages.WM_MOUSEWHEEL: MouseWheel?.Invoke(this, e); break;
                    }
                }
                catch
                {
                    // フック側では握って次へ
                }

                if (e.Handled)
                    return (IntPtr)1;
            }
            catch
            {
                // Marshal/PInvoke系の事故も握って次へ
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private const int WH_MOUSE_LL = 14;

        private enum MouseMessages
        {
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_MOUSEMOVE = 0x0200,
            WM_MOUSEWHEEL = 0x020A,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205,
            WM_MBUTTONDOWN = 0x0207,
            WM_MBUTTONUP = 0x0208,
            WM_XBUTTONDOWN = 0x020B,
            WM_XBUTTONUP = 0x020C
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }

    public struct MousePoint
    {
        public int X;
        public int Y;

        public MousePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
