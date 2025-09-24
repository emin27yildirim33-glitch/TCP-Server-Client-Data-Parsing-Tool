using System;
using System.IO;
using System.Windows.Forms;

namespace TcpTool;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => ShowAndLog(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown error");
            ShowAndLog(ex);
        };
        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            ShowAndLog(ex);
        }
    }

    private static void ShowAndLog(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n");
        }
        catch { }
        try
        {
            MessageBox.Show(ex.ToString(), "Unhandled exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { }
    }
}
