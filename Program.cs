namespace StockTracker;

using System.Threading;

static class Program
{
    private static Mutex? mutex = null;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        const string appName = "StockTracker_SingleInstance_Mutex";
        bool createdNew;

        mutex = new Mutex(true, appName, out createdNew);

        if (!createdNew)
        {
            // App is already running, exit immediately
            return;
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }    
}