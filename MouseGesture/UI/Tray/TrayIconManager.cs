using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
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
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                throw new FileNotFoundException("自動起動に登録する実行ファイルが見つかりません。", executablePath);
            }

            string taskFilePath = Path.Combine(Path.GetTempPath(), $"MouseGesture-{Guid.NewGuid():N}.xml");
            try
            {
                File.WriteAllText(taskFilePath, CreateStartupTaskXml(executablePath), new UnicodeEncoding(false, true));
                RunSchtasks("/Create", "/TN", StartupTaskName, "/XML", taskFilePath, "/F");
            }
            finally
            {
                try
                {
                    File.Delete(taskFilePath);
                }
                catch
                {
                    // 登録後の一時ファイル削除失敗は、自動起動設定の成否に影響させない。
                }
            }
        }

        private static string CreateStartupTaskXml(string executablePath)
        {
            string userSid = WindowsIdentity.GetCurrent().User?.Value;
            if (string.IsNullOrWhiteSpace(userSid))
            {
                throw new InvalidOperationException("現在のユーザーを取得できませんでした。");
            }

            string escapedSid = SecurityElement.Escape(userSid);
            string escapedPath = SecurityElement.Escape(Path.GetFullPath(executablePath));
            string escapedWorkingDirectory = SecurityElement.Escape(Path.GetDirectoryName(Path.GetFullPath(executablePath)));

            return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>MouseGesture をサインイン時に起動します。</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{escapedSid}</UserId>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{escapedSid}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{escapedPath}</Command>
      <Arguments>--autostart</Arguments>
      <WorkingDirectory>{escapedWorkingDirectory}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";
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
