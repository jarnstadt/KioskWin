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
        // F8: generate a password hash in CLI mode without starting the UI
        if (TryHandleHashPassword(args))
            return;

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

    private static bool TryHandleHashPassword(string[] args)
    {
        if (args.Length == 2 &&
            string.Equals(args[0], "--hash-password", StringComparison.OrdinalIgnoreCase))
        {
            // WinExe has no console — attach to parent console (PowerShell/cmd) or allocate one
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
                AllocConsole();

            var result = PasswordHasher.Generate(args[1]);
            Console.WriteLine("Add these two lines to appsettings.json:");
            Console.WriteLine("  \"AdminPasswordHash\": \"" + result.Hash + "\",");
            Console.WriteLine("  \"PasswordSalt\": \"" + result.Salt + "\"");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            FreeConsole();
            return true;
        }
        return false;
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

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;
    private const int SW_RESTORE = 9;
}
