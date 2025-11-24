using System;
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

    public class MouseHook
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

        private LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public MouseHook()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            _hookID = SetHook(_proc);
        }

        public void Stop()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        public bool Paused { get; set; }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !Paused)
            {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                MousePoint p = new MousePoint(hookStruct.pt.x, hookStruct.pt.y);
                MouseHookEventArgs e = new MouseHookEventArgs(p, (int)hookStruct.mouseData);

                if (MouseMessages.WM_LBUTTONDOWN == (MouseMessages)wParam)
                    LeftButtonDown?.Invoke(this, e);
                if (MouseMessages.WM_LBUTTONUP == (MouseMessages)wParam)
                    LeftButtonUp?.Invoke(this, e);
                if (MouseMessages.WM_RBUTTONDOWN == (MouseMessages)wParam)
                    RightButtonDown?.Invoke(this, e);
                if (MouseMessages.WM_RBUTTONUP == (MouseMessages)wParam)
                    RightButtonUp?.Invoke(this, e);
                if (MouseMessages.WM_MBUTTONDOWN == (MouseMessages)wParam)
                    MiddleButtonDown?.Invoke(this, e);
                if (MouseMessages.WM_MBUTTONUP == (MouseMessages)wParam)
                    MiddleButtonUp?.Invoke(this, e);
                if (MouseMessages.WM_XBUTTONDOWN == (MouseMessages)wParam)
                    XButtonDown?.Invoke(this, e);
                if (MouseMessages.WM_XBUTTONUP == (MouseMessages)wParam)
                    XButtonUp?.Invoke(this, e);
                if (MouseMessages.WM_MOUSEMOVE == (MouseMessages)wParam)
                    MouseMove?.Invoke(this, e);
                if (MouseMessages.WM_MOUSEWHEEL == (MouseMessages)wParam)
                    MouseWheel?.Invoke(this, e);

                if (e.Handled)
                {
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        // P/Invoke for simulating mouse events
        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        public const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const int MOUSEEVENTF_RIGHTUP = 0x0010;

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
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

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
