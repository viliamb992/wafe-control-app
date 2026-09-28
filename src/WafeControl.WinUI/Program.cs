using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;
using WafeControl.Core.Diagnostics;
using WafeControl.WinUI.Services;

namespace WafeControl.WinUI;

/// <summary>
/// Custom entry point (DISABLE_XAML_GENERATED_MAIN) that keeps the app single-instance:
/// a second launch signals the running instance, which brings its window back from the tray, and exits.
/// </summary>
public static partial class Program
{
    // One per Windows sign-in session, wherever the exe is. (AppInstance keys of an unpackaged app depend on the
    // exe's path, so they don't stop a copy of the app in another folder.)
    private const string ActivateSignalName = @"Local\WafeControl.Main";

    // ASFW_ANY: the running instance's process id isn't known here.
    private const uint AnyProcess = uint.MaxValue;

    private static EventWaitHandle? _activateSignal;

    // Null until the app is created; a launch signalled before then finds the window opening anyway.
    private static App? _app;

    [STAThread]
    private static int Main(string[] args)
    {
        // First: the installer and updater start the app with special arguments, handled here and exited.
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => RegistryStartupRegistration.RemoveEntries())
            .Run();

        // Crashes anywhere from here on leave a log entry and a marker for the next start.
        LegacyInstallMigration.MoveLogs(App.LogDirectory);
        CrashHandler.Initialize(App.LogDirectory);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
                CrashHandler.OnFatal(exception, "AppDomain");
        };
        TaskScheduler.UnobservedTaskException += CrashHandler.OnUnobservedTask;

        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (RedirectToRunningInstance())
            return 0;

        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _app = new App();
        });

        return 0;
    }

    /// <returns>True if another instance owns the app and this process should exit.</returns>
    private static bool RedirectToRunningInstance()
    {
        bool createdNew;
        try
        {
            _activateSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateSignalName, out createdNew);
        }
        catch (UnauthorizedAccessException)
        {
            // Held by an instance running as administrator, which this one can't signal. Still only one runs.
            Debug.WriteLine("Another instance runs elevated; exiting");
            return true;
        }

        if (createdNew)
        {
            // Stays registered for the whole run; each later launch sets the signal once.
            ThreadPool.RegisterWaitForSingleObject(_activateSignal, (_, _) => _app?.ActivateMainWindow(), null, Timeout.Infinite, executeOnlyOnce: false);
            return false;
        }

        // Let the running instance take the foreground; this process holds the right because the user just launched it.
        AllowSetForegroundWindow(AnyProcess);
        _activateSignal.Set();
        _activateSignal.Dispose();

        Debug.WriteLine("Asked the running instance to show its window");
        return true;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(uint processId);
}
