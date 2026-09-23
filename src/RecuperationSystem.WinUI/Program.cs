using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace RecuperationSystem.WinUI;

/// <summary>
/// Custom entry point (DISABLE_XAML_GENERATED_MAIN) that keeps the app single-instance:
/// a second launch hands its activation to the running instance, which brings its window back from the tray.
/// </summary>
public static partial class Program
{
    private const string InstanceKey = "RecuperationSystem.Main";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (RedirectToRunningInstance())
            return 0;

        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });

        return 0;
    }

    /// <returns>True if another instance owns the app and this process should exit.</returns>
    private static bool RedirectToRunningInstance()
    {
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (mainInstance.IsCurrent)
        {
            mainInstance.Activated += (_, _) => App.Current.ActivateMainWindow();
            return false;
        }

        // Let the running instance take the foreground; this process holds the right because the user just launched it.
        AllowSetForegroundWindow(mainInstance.ProcessId);

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        Task.Run(() => mainInstance.RedirectActivationToAsync(activation).AsTask()).Wait();

        Debug.WriteLine($"Redirected activation to running instance (PID {mainInstance.ProcessId})");
        return true;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(uint processId);
}
