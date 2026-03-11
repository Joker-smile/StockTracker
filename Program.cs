using Avalonia;
using System;
using System.Threading;

namespace StockTracker;

class Program
{
    private static Mutex? mutex = null;

    [STAThread]
    public static void Main(string[] args)
    {
        const string appName = "StockTracker_SingleInstance_Mutex";
        bool createdNew;
        mutex = new Mutex(true, appName, out createdNew);

        if (!createdNew)
        {
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}