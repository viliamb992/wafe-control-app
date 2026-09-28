using Android.App;
using Android.Runtime;
using WafeControl.Core.Diagnostics;

namespace WafeControl.Mobile;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        // .NET exceptions that reach the Android side; not handled, so the app still ends (as it must).
        AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => CrashHandler.OnFatal(e.Exception, "Android");
        base.OnCreate();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
