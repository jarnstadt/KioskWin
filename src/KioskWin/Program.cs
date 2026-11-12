using System.Diagnostics;
using System.Runtime.InteropServices;
using KioskWin.Core;
using KioskWin.Forms;

namespace KioskWin;

internal static class Program
{
    private const string MutexName = "Global\\KioskWin_SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--hash-password", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("The --hash-password option is no longer supported. Set AdminPassword in appsettings.json.",
                "KioskWin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ApplicationConfiguration.Initialize();

        // F9: single instance — bring the existing instance to the foreground and exit
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            BringExistingToFront();
            return;
        }

        // N3: handle unhandled exceptions globally to avoid silent crashes
        Application.ThreadException += (_, e) => LogException(e.Exception, "UI thread exception");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogException(e.ExceptionObject as Exception, "AppDomain unhandled");

        try
        {
            var logger = new FileLogger();
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var config = KioskConfig.LoadFromFile(configPath);
            logger.Log($"[startup] url={config.Url} topMost={config.TopMost}");
            Application.Run(new MainForm(config, logger));
        }
        catch (Exception ex)
        {
            LogException(ex, "startup");
            MessageBox.Show(
                "The program failed to start. Check the log:\n" + ex.Message,
                "KioskWin",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void BringExistingToFront()
    {
        try
        {
            var current = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcessesByName(current.ProcessName))
            {
                if (p.Id == current.Id || p.MainWindowHandle == IntPtr.Zero) continue;
                SetForegroundWindow(p.MainWindowHandle);
                ShowWindow(p.MainWindowHandle, SW_RESTORE);
            }
        }
        catch
        {
            // Best effort: failure to bring the window forward does not affect single-instance exit logic.
        }
    }

    private static void LogException(Exception? ex, string source)
    {
        try
        {
            new FileLogger().Log($"[{source}] {ex}");
        }
        catch
        {
            // Do not rethrow logging failures and cause a second exception.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;
}
