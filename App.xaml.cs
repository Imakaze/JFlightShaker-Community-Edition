using JFlightShaker.Service;
using System.Windows;

namespace JFlightShaker;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppLog.Initialize();
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled UI exception.", args.Exception);
            MessageBox.Show(
                $"JFlightShaker encountered an unexpected error.\n\n" +
                $"A diagnostic log was saved to:\n{AppLog.LogPath}\n\n" +
                args.Exception.Message,
                "JFlightShaker error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error(
                "Unhandled application exception.",
                args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved background task exception.", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);
    }
}
