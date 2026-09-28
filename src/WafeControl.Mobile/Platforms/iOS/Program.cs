using ObjCRuntime;
using UIKit;
using WafeControl.Core.Diagnostics;

namespace WafeControl.Mobile;

public class Program
{
    // This is the main entry point of the application.
    static void Main(string[] args)
    {
        // .NET exceptions crossing into Objective-C end the app; record them first.
        Runtime.MarshalManagedException += (_, e) => CrashHandler.OnFatal(e.Exception, "iOS");

        // if you want to use a different Application Delegate class from "AppDelegate"
        // you can specify it here.
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
