using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = @"D:\works\VisualStudio\WindowsFormsApp1\MouseGesture\bin\Debug\MouseGesture.exe",
            WorkingDirectory = @"C:\Windows\System32",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        try
        {
            using (var process = Process.Start(psi))
            {
                process.WaitForExit();
                Console.WriteLine("Exit Code: " + process.ExitCode);
                Console.WriteLine("Output: " + process.StandardOutput.ReadToEnd());
                Console.WriteLine("Error: " + process.StandardError.ReadToEnd());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to start: " + ex.Message);
        }
    }
}
