using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public class TrayIconManager : IDisposable
    {
        private const string AppName = "MouseGesture";
        private const string StartupTaskName = @"\MouseGesture";
        private const string RunRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

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
            RegisterAutoStartForExecutable(enable, executablePath);
        }

        public static void RegisterAutoStartForExecutable(bool enable, string executablePath)
        {
            try
            {
                DeleteRegistryAutoStart();

                if (enable)
                {
                    CreateStartupTask(executablePath);
                }
                else
                {
                    DeleteStartupTask();
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"スタートアップの設定に失敗しました。管理者権限で起動してから再度お試しください: {ex.Message}", ex);
            }
        }

        public bool GetAutoStartStatus(string executablePath)
        {
            return GetAutoStartStatusForExecutable(executablePath);
        }

        public static bool GetAutoStartStatusForExecutable(string executablePath)
        {
            return StartupTaskExists() || RegistryAutoStartExists(executablePath);
        }

        private static void CreateStartupTask(string executablePath)
        {
            string taskCommand = $"\"{executablePath}\" --autostart";

            RunSchtasks(
                "/Create",
                "/TN", StartupTaskName,
                "/SC", "ONLOGON",
                "/TR", taskCommand,
                "/RL", "HIGHEST",
                "/F");
        }

        private static void DeleteStartupTask()
        {
            if (!StartupTaskExists()) return;

            RunSchtasks("/Delete", "/TN", StartupTaskName, "/F");
        }

        private static bool StartupTaskExists()
        {
            return RunSchtasks(false, "/Query", "/TN", StartupTaskName) == 0;
        }

        private static void RunSchtasks(params string[] arguments)
        {
            int exitCode = RunSchtasks(true, arguments);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"schtasks.exe failed with exit code {exitCode}.");
            }
        }

        private static int RunSchtasks(bool throwOnStartFailure, params string[] arguments)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "schtasks.exe";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    foreach (string argument in arguments)
                    {
                        process.StartInfo.ArgumentList.Add(argument);
                    }

                    process.Start();
                    process.WaitForExit();
                    return process.ExitCode;
                }
            }
            catch
            {
                if (throwOnStartFailure) throw;
                return -1;
            }
        }

        private static void DeleteRegistryAutoStart()
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
            {
                key?.DeleteValue(AppName, false);
            }
        }

        private static bool RegistryAutoStartExists(string executablePath)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryPath, false))
                {
                    object value = key?.GetValue(AppName);
                    if (value == null) return false;

                    string expectedPath = $"\"{executablePath}\"";
                    string valueText = value.ToString();
                    return valueText.StartsWith(expectedPath, StringComparison.OrdinalIgnoreCase)
                        || valueText.StartsWith(executablePath, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
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
