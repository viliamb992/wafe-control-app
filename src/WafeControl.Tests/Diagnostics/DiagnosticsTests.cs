using Microsoft.Extensions.Logging;
using Sentry;
using WafeControl.Core.Diagnostics;
using WafeControl.Core.Localization;
using WafeControl.Core.Threading;
using WafeControl.Shared.Diagnostics;
using WafeControl.Shared.Services;

namespace WafeControl.Tests.Diagnostics;

public class LogRedactionTests
{
    [Theory]
    [InlineData("viliam.birmon@gmail.com", "v***@g***.com")]
    [InlineData("a@b.cz", "a***@b***.cz")]
    [InlineData("demo", "d***")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Email_KeepsOnlyTheFirstLetters(string? email, string expected) =>
        Assert.Equal(expected, LogRedaction.Email(email));

    [Fact]
    public void Scrub_ReplacesEveryAddressInText() =>
        Assert.Equal("Login for a***@e***.com failed, cc b***@e***.org",
            LogRedaction.Scrub("Login for alice@example.com failed, cc bob@example.org"));
}

public class CrashReportingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    public CrashReportingTests() => CrashReporting.IsAllowed = () => true;

    public void Dispose() => CrashReporting.IsAllowed = () => false;

    [Fact]
    public void BeforeSend_WithoutConsent_DropsTheEvent()
    {
        CrashReporting.IsAllowed = () => false;

        Assert.Null(CrashReporting.BeforeSend(new SentryEvent(new InvalidOperationException("x")), Now));
    }

    [Fact]
    public void BeforeSend_ScrubsEmails()
    {
        var evt = new SentryEvent(new InvalidOperationException("no user alice@example.com"))
        {
            Message = new SentryMessage { Message = "Sign in for {User}", Formatted = "Sign in for alice@example.com" },
        };
        evt.SetExtra("User", "alice@example.com");

        var sent = CrashReporting.BeforeSend(evt, Now.AddHours(1))!;

        Assert.Equal("Sign in for a***@e***.com", sent.Message!.Formatted);
        Assert.Equal("a***@e***.com", sent.Extra["User"]);
    }

    [Fact]
    public void BeforeSend_SameErrorWithinTheWindow_IsSentOnce()
    {
        SentryEvent Event() => new() { Message = new SentryMessage { Message = "Repeated " + nameof(BeforeSend_SameErrorWithinTheWindow_IsSentOnce) } };

        Assert.NotNull(CrashReporting.BeforeSend(Event(), Now));
        Assert.Null(CrashReporting.BeforeSend(Event(), Now.AddMinutes(5)));
        Assert.NotNull(CrashReporting.BeforeSend(Event(), Now.AddMinutes(11)));
    }

    [Fact]
    public void BeforeSend_Crashes_AreNeverHeldBack()
    {
        SentryEvent Crash() => new() { Level = SentryLevel.Fatal, Message = new SentryMessage { Message = "Crash " + nameof(BeforeSend_Crashes_AreNeverHeldBack) } };

        Assert.NotNull(CrashReporting.BeforeSend(Crash(), Now));
        Assert.NotNull(CrashReporting.BeforeSend(Crash(), Now));
    }

    [Fact]
    public void BeforeSend_UnreadableResponse_IsGroupedByEndpoint()
    {
        var evt = new SentryEvent
        {
            Message = new SentryMessage { Message = WafeApiService.InvalidResponseMessage, Formatted = "API response from api/v1/main could not be read" },
        };

        var sent = CrashReporting.BeforeSend(evt, Now.AddDays(1))!;

        Assert.Equal(["api-drift", "API response from api/v1/main could not be read"], sent.Fingerprint);
    }

    [Theory]
    [InlineData("0.0.0-dev", "dev")]
    [InlineData("1.3.0-beta.1", "beta")]
    [InlineData("1.3.0", "production")]
    public void EnvironmentName_FollowsTheVersion(string version, string expected) =>
        Assert.Equal(expected, CrashReporting.EnvironmentName(version));
}

public class ProblemReportTests
{
    private static readonly DeviceDescription Device = new("windows", "Windows 11 26200", "x64", null, "velopack");

    [Fact]
    public void Text_DescribesAppAndDevice()
    {
        var report = ProblemReport.Create(Device, "cs", isDemo: true, unitModel: "W0201", crash: null);

        Assert.Contains("Platform: windows (x64)", report.Text);
        Assert.Contains("Demo mode: yes", report.Text);
        Assert.Contains("Unit model: W0201", report.Text);
    }

    [Fact]
    public void IssueUrl_FillsTheBugForm()
    {
        var crash = new CrashRecord(DateTimeOffset.Now, "1.2.0", "XAML", "System.InvalidOperationException", "boom", "", "abc123");

        var url = ProblemReport.Create(Device, "en", isDemo: false, unitModel: null, crash).IssueUrl.AbsoluteUri;

        Assert.StartsWith("https://github.com/viliamb992/wafe-control-app/issues/new?template=bug.yml", url);
        Assert.Contains("platform=windows", url);
        Assert.Contains("crash-id=abc123", url);
    }
}

public class ErrorTextTests
{
    [Fact]
    public void EveryError_HasAMessage() =>
        Assert.All(Enum.GetValues<ApiError>().Where(e => e != ApiError.None),
            error => Assert.False(string.IsNullOrWhiteSpace(ErrorText.For(error))));

    [Fact]
    public void OnlyNetworkAndServerErrors_AreRetryable()
    {
        Assert.True(ErrorText.IsRetryable(ApiError.Offline));
        Assert.True(ErrorText.IsRetryable(ApiError.Timeout));
        Assert.False(ErrorText.IsRetryable(ApiError.Unauthorized));
        Assert.False(ErrorText.IsRetryable(ApiError.Rejected));
    }
}

public class TaskExtensionsTests
{
    [Fact]
    public async Task Forget_LogsAFailure()
    {
        var logger = new ListLogger();
        var failing = Task.FromException(new InvalidOperationException("boom"));

        failing.Forget(logger, "Background work");
        await Task.Yield();

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task Forget_LogsAFailureThatComesLater()
    {
        var logger = new ListLogger();
        var work = new TaskCompletionSource();

        work.Task.Forget(logger, "Background work");
        work.SetException(new InvalidOperationException("later"));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Single(logger.Entries);
    }

    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Entries)
                Entries.Add((logLevel, exception));
        }
    }
}

public class WindowsCrashEventTests
{
    private static readonly DateTimeOffset Time = new(2026, 9, 28, 10, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void ApplicationError_DescribesTheFaultingModule()
    {
        // As Windows writes it (Application log, event 1000).
        string[] data =
        [
            "WafeControl.WinUI.exe", "1.2.0.57", "67c872fb", "Microsoft.ui.xaml.dll", "3.1.7.0", "2bbefd73",
            "c000027b", "00000000000a527e", "14432", "134346912704665478",
            @"C:\Users\x\AppData\Local\WafeControl.App\current\WafeControl.WinUI.exe", @"C:\...\Microsoft.ui.xaml.dll",
            "e231c10a-e9af-4bce-9b2b-1e53f432b311", "", "",
        ];

        var crash = WindowsCrashEvent.TryParse(WindowsCrashEvent.ApplicationError, data, Time, "WafeControl.WinUI.exe");

        Assert.NotNull(crash);
        Assert.Equal("0xc000027b in Microsoft.ui.xaml.dll", crash.ExceptionType);
        Assert.Equal("1.2.0.57", crash.Version);
        Assert.Equal(Time, crash.Time);
    }

    [Fact]
    public void ApplicationError_OfAnotherApp_IsIgnored()
    {
        string[] data = ["netsimd.exe", "0.0.0.0", "67c872fb", "ucrtbase.dll", "10.0.26100.9444", "2bbefd73", "c0000409", "0000000000000000"];

        Assert.Null(WindowsCrashEvent.TryParse(WindowsCrashEvent.ApplicationError, data, Time, "WafeControl.WinUI.exe"));
    }

    [Fact]
    public void DotNetRuntime_ReadsTheExceptionAndStack()
    {
        string[] data =
        [
            """
            Application: WafeControl.WinUI.exe
            CoreCLR Version: 10.0.1226.42308
            .NET Version: 10.0.12
            Description: The process was terminated due to stack overflow.
            Exception Info: System.InsufficientExecutionStackException: Insufficient stack to continue.
               at WafeControl.Core.Foo.Bar()
               at WafeControl.Core.Foo.Baz()
            """,
        ];

        var crash = WindowsCrashEvent.TryParse(WindowsCrashEvent.DotNetRuntime, data, Time, "WafeControl.WinUI.exe");

        Assert.NotNull(crash);
        Assert.Equal("System.InsufficientExecutionStackException", crash.ExceptionType);
        Assert.Equal("Insufficient stack to continue.", crash.Message);
        Assert.Equal("at WafeControl.Core.Foo.Bar()\nat WafeControl.Core.Foo.Baz()", crash.StackTrace);
    }
}
