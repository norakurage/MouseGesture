using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    internal static class Program
    {
        static Mutex _mutex;

        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                Environment.CurrentDirectory = AppContext.BaseDirectory;

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    try
                    {
                        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log");
                        System.IO.File.AppendAllText(path,
                            DateTime.Now + Environment.NewLine +
                            e.ExceptionObject.ToString() +
                            Environment.NewLine + Environment.NewLine);
                    }
                    catch { }
                };

                const string mutexName = "MouseGestureApp_Mutex";
                const string eventName = "MouseGestureApp_ShowWindowEvent";

                bool createdNew;
                _mutex = new Mutex(true, mutexName, out createdNew);

                if (createdNew)
                {
                    // 1つ目のインスタンス
                    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Form1(args));
                    _mutex.ReleaseMutex();
                }
                else
                {
                    // 2つ目以降のインスタンスが起動された場合は、元のインスタンスにシグナルを送る
                    try
                    {
                        EventWaitHandle showEvent = EventWaitHandle.OpenExisting(eventName);
                        showEvent.Set();
                    }
                    catch (WaitHandleCannotBeOpenedException)
                    {
                        // ハンドルが開けない場合は無視
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"),
                        ex.ToString());
                }
                catch { }
            }
        }
    }
}
