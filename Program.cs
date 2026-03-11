using Avalonia;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StockTracker;

class Program
{
    private static Mutex? mutex = null;

    [STAThread]
    public static void Main(string[] args)
    {
        SetupExceptionHandling();

        try
        {
            // Named Mutex only works reliably on Windows.
            // On macOS/Linux, named mutexes can leave stale files and silently prevent launch.
            if (OperatingSystem.IsWindows())
            {
                const string appName = "StockTracker_SingleInstance_Mutex";
                bool createdNew;
                mutex = new Mutex(true, appName, out createdNew);

                if (!createdNew)
                {
                    return;
                }
            }

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogError("Fatal error in Main", ex);
        }
    }

    private static void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogError("AppDomain UnhandledException", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogError("TaskScheduler UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    public static void LogError(string context, Exception? ex)
    {
        try
        {
            try 
            {
                // Try to write to the local directory first for true "green software" portability
                string? exePath = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName);
                string localFile = Path.Combine(exePath ?? AppContext.BaseDirectory, "error_log.txt");
                string msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}]\r\n{ex?.ToString() ?? "Unknown Error"}\r\n----------------------------------------\r\n";
                File.AppendAllText(localFile, msg);
                return; // Success
            }
            catch (UnauthorizedAccessException) { }

            // Fallback for strict permissions (macOS bundles, C:\Program Files, etc.)
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logDir = Path.Combine(appData, "StockTracker");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            
            string logFile = Path.Combine(logDir, "error_log.txt");
            string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}]\r\n{ex?.ToString() ?? "Unknown Error"}\r\n----------------------------------------\r\n";
            File.AppendAllText(logFile, message);
            
            // In case of fatal crash on macOS, write to desktop as absolute fallback
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    string desktopLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "StockTracker_Crash.txt");
                    File.AppendAllText(desktopLog, message);
                }
                catch { }
            }
        }
        catch { }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // 为保证最大兼容性，在所有 Windows 10/11 版本上统一使用软件渲染，避免任何可能的硬件加速死锁或花屏问题
        if (OperatingSystem.IsWindows())
        {
            builder.With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Software }
            });
        }

        return builder;
    }
}