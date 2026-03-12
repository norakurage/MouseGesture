using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public class TrayIconManager : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private Action _onRestoreRequest;
        private Action _onExitRequest;

        // 本当に終了する場合のみ true (MainForm 側と連動)
        public bool RealClose { get; set; } = false;

        public TrayIconManager(Icon icon, string text, Action onRestoreRequest, Action onExitRequest)
        {
            _onRestoreRequest = onRestoreRequest;
            _onExitRequest = onExitRequest;

            InitializeTrayIcon(icon, text);
        }

        private void InitializeTrayIcon(Icon icon, string text)
        {
            var menu = new ContextMenuStrip();
            var menuOpen = new ToolStripMenuItem("設定を開く");
            menuOpen.Click += (s, e) => _onRestoreRequest?.Invoke();
            menu.Items.Add(menuOpen);

            menu.Items.Add(new ToolStripSeparator());

            var menuExit = new ToolStripMenuItem("終了");
            menuExit.Click += (s, e) =>
            {
                RealClose = true;
                _onExitRequest?.Invoke();
            };
            menu.Items.Add(menuExit);

            _notifyIcon = new NotifyIcon
            {
                Text = text,
                Icon = icon ?? SystemIcons.Application,
                ContextMenuStrip = menu,
                Visible = true
            };

            // ダブルクリックで設定ウィンドウを表示
            _notifyIcon.DoubleClick += (s, e) => _onRestoreRequest?.Invoke();
        }

        public void RegisterAutoStart(bool enable, string executablePath)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (enable)
                    {
                        key.SetValue("MouseGesture", $"\"{executablePath}\"");
                    }
                    else
                    {
                        key.DeleteValue("MouseGesture", false);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"スタートアップの設定に失敗しました: {ex.Message}", ex);
            }
        }

        public bool GetAutoStartStatus(string executablePath)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("MouseGesture");
                        if (value != null)
                        {
                            string expectedPath = $"\"{executablePath}\"";
                            return value.ToString() == expectedPath || value.ToString() == executablePath;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public void HideTrayIcon()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
