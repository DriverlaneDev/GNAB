namespace Gnab;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogError(e.ExceptionObject as Exception);

        using var mutex = new Mutex(true, "Gnab.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("GNAB is already running (check the system tray).",
                "Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayApplicationContext());
    }

    private static void LogError(Exception? ex)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gnab");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "error.log"), $"{DateTime.Now:u} {ex}\n\n");
        }
        catch
        {
            // Logging is best-effort; never let a logging failure cascade.
        }
    }
}
