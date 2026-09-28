# Plan: crash tracking

Goal: when someone else's copy of the app crashes or misbehaves, you find out, with enough context to fix it. Everything is free, users opt in, and the data stays private.

Scope:

- **Android and Windows:** local logs, crash handling, and **opt-in remote crash reports** through the free GlitchTip service ([Phase 5](#phase-5-remote-crash-reports-via-glitchtip-m)), using the Sentry SDK on both.
- **Windows extra:** WinUI native crashes that bypass .NET are picked up from the Windows event log on the next start ([Phase 6](#phase-6-windows-native-crashes-from-the-event-log-s)).
- **iOS:** gets the Android setup when it ships, because the SDK is cross-platform.

Status: **implemented** on branch `crash-tracking-ux` (2026-09-28), except the [last step](#last-step-you-connect-glitchtip), which needs your GlitchTip DSN. Revised before implementation: GlitchTip instead of Sentry (free), used for Android and Windows.

Where the code is: `src/WafeControl.Core/Diagnostics` (logging, crash marker, GlitchTip options, problem report, Windows crash events), `src/WafeControl.Core/Threading` (`SafeAsync`, `Forget`), `src/WafeControl.WinUI/Services` (`WindowsCrashReports`, `NativeCrashDetector`), `src/WafeControl.Mobile/Services/MobileCrashReports.cs`, and the tests in `src/WafeControl.Tests/Diagnostics`.

Differences from the plan below:

- The Debug-only crash command is a button, not "tap the version 7 times": Settings → "Demo unit faults (Debug build)" on Windows has UI thread, background thread and native (fail fast) crashes; Android has "Crash (test)" under About.
- Debug builds always report as environment `dev`, so a local test with a DSN doesn't mix with real reports.

## Where we are

| Area | Windows (WinUI) | Mobile (MAUI) |
| --- | --- | --- |
| Log file | Serilog, `%LocalAppData%\WafeControl\logs`, 7 days | **None in Release** (`AddDebug` only in DEBUG, [MauiProgram.cs](../../src/WafeControl.Mobile/MauiProgram.cs)) |
| Unhandled exceptions | Only `Application.UnhandledException` ([App.xaml.cs](../../src/WafeControl.WinUI/App.xaml.cs)); no `AppDomain` / `TaskScheduler` hooks; log not flushed on a crash | None |
| Native crashes | Not seen at all (e.g. XAML stowed exception `0xc000027b`) | Not seen at all |
| `async void` | `OnLaunched`, 3 view handlers | ~25 page handlers with no try/catch: any exception kills the app |
| Symbols | `-p:DebugType=None` in [release.yml](../../.github/workflows/release.yml): no line numbers | Trimmed + R8, no symbols kept |
| Remote reporting | None | None |

Log levels are noisy too. Every failed poll logs an **Error**, e.g. `GET … failed` in [WafeApiService.cs](../../src/WafeControl.Shared/Services/WafeApiService.cs), which is every 5 s while offline. Before Error logs become reported events, expected failures have to become Warnings. Otherwise one offline tray app or phone would use up the free monthly quota in a day.

## Phases

Each phase can ship on its own. Phases 1–4 need no server and are worth doing even if remote reporting never happens.

### Phase 1: logs (S)

Windows already has logs; it only picks up the shared setup and the redaction.

1. **Shared logger setup.** Add `Serilog`, `Serilog.Extensions.Logging` and `Serilog.Sinks.File` to Core, plus `Core/Diagnostics/AppLogging.cs`:
   - `CreateLogger(string logDirectory)` holds the config now in `App.CreateLogger()` (daily files, 7 files kept, `fileSizeLimitBytes: 2 MB`, `rollOnFileSizeLimit: true`, the same template and overrides).
   - `LogEnvironment(ILogger, string platform)` writes one line at startup: app version, platform, OS version, architecture, device model (mobile), UI language, and whether demo mode is on (see [ux-improvements.md](ux-improvements.md)).
2. **Windows:** call `AppLogging.CreateLogger(App.LogDirectory)`; nothing else changes.
3. **Mobile:** in `MauiProgram`, log to `Path.Combine(FileSystem.AppDataDirectory, "logs")` with `builder.Logging.AddSerilog(dispose: true)`. Keep `AddDebug` in DEBUG.
4. **Hide emails in logs.** Add `LogRedaction.Email("viliam.birmon@gmail.com")` → `"v***@g***.com"` in Core. Use it wherever `{Username}` is logged: `AuthenticationService` (4×) and `WafeApiService.AuthenticateAsync`. Test: `LogRedactionTests`. This matters on both platforms: logs become breadcrumbs in reports, and users attach log files to public GitHub issues.
5. **Log level rules** (write them into the README "Where data is stored → Logs" section):
   - `Warning`: expected operational failures, such as no network, timeouts, HTTP 4xx/5xx, and a command the unit didn't confirm.
   - `Error`: something that should not happen, such as a JSON parse failure, an exception in a view model, or a failed settings/credential write.
   - `Fatal`: the process is going down.

   In `WafeApiService`, `HttpRequestException`, timeouts and non-success statuses become Warning. `JsonException` stays Error, with a message template that names the endpoint (it becomes the "API changed" signal in Phase 5). Same in `SandcastleAuthHandler.TryRenewAsync`.
6. **Mobile "Share logs"** in Settings → About: zip the log folder to `FileSystem.CacheDirectory` and open the share sheet (`Share.RequestAsync(new ShareFileRequest(...))`). New strings `SettingsShareLogs` in cs/sk/en. (Windows already has ⋯ → "Open log folder".)

Done when: an Android Release build writes `wafe-*.log` and Share logs sends it to email or Drive; no email address appears unmasked in any log.

### Phase 2: global exception hooks and crash marker (S)

1. **Core `Diagnostics/CrashHandler.cs`** (static, no UI):
   - `OnFatal(Exception ex, string source)` logs `Fatal` with the source. After Phase 5 it then calls `SentrySdk.Flush(TimeSpan.FromSeconds(2))`, so the report leaves before the process dies. Then `Log.CloseAndFlush()`, then it writes `last-crash.json` (time, version, source, exception type, message, first 20 stack frames, and the reported event id once Phase 5 exists) next to the logs. It writes synchronously and never throws.
   - `TryTakeLastCrash()` reads that file and deletes it, for the next start.
   - `OnUnobservedTask(object?, UnobservedTaskExceptionEventArgs e)` logs Error and calls `e.SetObserved()`.
2. **Windows** ([Program.cs](../../src/WafeControl.WinUI/Program.cs) / [App.xaml.cs](../../src/WafeControl.WinUI/App.xaml.cs)):
   - In `Main` before `Application.Start`: `AppDomain.CurrentDomain.UnhandledException` → `CrashHandler.OnFatal(..., "AppDomain")`, and `TaskScheduler.UnobservedTaskException` → `OnUnobservedTask`.
   - Keep `Application.UnhandledException`, but route it to `OnFatal(..., "XAML")`. Don't set `e.Handled`: after an unknown exception the UI state can't be trusted. A crash with a report is better than a zombie tray app.
   - Wrap the body of `async void OnLaunched` in try/catch → `OnFatal`, then `Exit()`.
3. **Mobile:**
   - Hook `AppDomain` and `TaskScheduler` in `MauiProgram.CreateMauiApp` (before `builder.Build()`).
   - Android `MainApplication.OnCreate`: `AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => CrashHandler.OnFatal(e.Exception, "Android")`. Don't set `e.Handled`.
   - iOS `Program.Main`: `ObjCRuntime.Runtime.MarshalManagedException += (_, e) => CrashHandler.OnFatal(e.Exception, "iOS")`.
4. **Next start:** if `TryTakeLastCrash()` returns something (or, on Windows, Phase 6 finds a native crash), show once (after the first screen is up) "WAFE Control closed unexpectedly last time." with buttons [Report a problem] [Close]. Report a problem is described in [ux-improvements.md](ux-improvements.md#8-report-a-problem). WinUI uses a `ContentDialog`; mobile uses `DisplayAlertAsync`. When crash reports are on (Phase 5), the dialog adds "A crash report was sent automatically." New strings: `CrashDialogTitle`, `CrashDialogMessage`, `CrashDialogReport`, `CrashDialogReportSent`.

Done when: a DEBUG-only crash command (Settings → About, e.g. tap the version 7 times, compiled only `#if DEBUG`) crashes the app, the log ends with the Fatal entry, and the next start shows the dialog. Test it on UI thread, background thread and unobserved task, on both apps.

### Phase 3: no more crashing `async void` (S–M)

1. **Analyzer.** Add `Microsoft.VisualStudio.Threading.Analyzers` as a `GlobalPackageReference` in [Directory.Packages.props](../../Directory.Packages.props). In [.editorconfig](../../.editorconfig), turn off every `VSTHRD*` rule except these, set to `warning`:
   - `VSTHRD100`: avoid `async void`.
   - `VSTHRD101`: avoid async lambdas passed to void delegates.
   - `VSTHRD110`: observe the result of async calls.

   Justified exceptions get a `#pragma` with a reason (the `.Wait()` in `Program.RedirectToRunningInstance`).
2. **One helper per UI** (a small duplicate, because the platforms differ):
   - Mobile `Helpers/SafeAsync.cs`: `static async void Run(Func<Task> action, [CallerMemberName] string? caller = null)` awaits the action; on exception it logs Error with the caller name and shows the generic localized message `ErrorUnexpected` through the page toast. `OperationCanceledException` is ignored.
   - WinUI `Helpers/SafeAsync.cs`: the same, showing the message in the footer status.
3. **Convert every handler** found by `grep -rn "async void" src`:
   ```csharp
   // Before
   private async void OnCopyClicked(object? sender, EventArgs e) { ... }
   // After
   private void OnCopyClicked(object? sender, EventArgs e) => SafeAsync.Run(CopyAsync);
   ```
   `StatusToast.Show` and `AppShell.UpdateRoute` get the same treatment. Overrides such as `OnAppearing` keep `async void` (the signature is fixed), with the body inside `SafeAsync.Run`.
4. **Fire-and-forget in Core.** Add `Core/Threading/TaskExtensions.Forget(this Task task, ILogger logger, string operation)`, which attaches a continuation that logs a fault. Replace `_ = XAsync()` in `AppViewModel`, `FlowSpeedCardViewModel` and `DashboardPage` (`TrackUnitModeAsync`). Test: a faulted task logs once and never raises `UnobservedTaskException`.
5. **Build policy.** Add a root `Directory.Build.props` with `<TreatWarningsAsErrors Condition="'$(ContinuousIntegrationBuild)' == 'true'">true</TreatWarningsAsErrors>`, so CI fails on new VSTHRD warnings while local builds stay friendly. Set `ContinuousIntegrationBuild=true` in the workflows via `-p:` or the `CI` env var.

Done when: the build shows no VSTHRD100/101/110 warnings, and an exception thrown on purpose in any page handler shows the "Something went wrong" message instead of crashing.

### Phase 4: release builds with line numbers (S)

1. **Windows:** in [release.yml](../../.github/workflows/release.yml), replace `-p:DebugType=None -p:DebugSymbols=false` with `-p:DebugType=embedded`. The PDB goes inside the app's own DLLs (a few hundred KB in total; framework assemblies are unaffected). .NET then resolves file and line on the user's machine, so both the local log and the GlitchTip report (Phase 5) show them. GlitchTip doesn't need to symbolicate anything.
2. **Android:** add `<DebugType>portable</DebugType>` for Release in the Mobile csproj, and upload the PDBs and the R8 `mapping.txt` as a workflow artifact (`android-symbols`, kept 90 days). Android doesn't use PDBs on the device and GlitchTip doesn't symbolicate .NET, so Android reports show method names but no line numbers. For the rare case that needs one, match the PDB from the artifact by hand. The mapping file de-obfuscates the Java frames of the Sentry Android SDK.

Done when: a test crash from an installed Windows release shows `App.xaml.cs:line N` in the log.

### Phase 5: remote crash reports via GlitchTip (M)

#### Choice: GlitchTip, through the Sentry SDK

[GlitchTip](https://glitchtip.com) is an open-source (MIT) error tracker that accepts the **Sentry SDK's** protocol. Both apps use the well-maintained Sentry .NET packages, and only their DSNs point at GlitchTip.

- **Free:** the hosted free plan (about 1,000 events/month at the time of writing; check current limits). The limit is probably **per organization**, so Windows and Android share it. That is plenty for a few users once Phase 1's log level rules and the quota guard below are in. If it's ever not enough, self-host GlitchTip (Docker + Postgres) for free, or switch to Sentry, by changing only the DSNs.
- **Catches:**
  - Android: managed .NET crashes, Java crashes, native crashes and ANRs (through the Android SDK bundled in `Sentry.Maui`), plus non-fatal Error logs.
  - Windows: managed .NET crashes (all three hooks from Phase 2) and non-fatal Error logs. Native WinUI crashes bypass .NET and are handled by Phase 6.
- **Not included** (compared with Sentry): release health / crash-free sessions, server-side .NET symbolication, rich user feedback. Neither is needed at this scale, and Windows gets line numbers anyway (Phase 4).

Alternatives considered:

| Option | Why not (first choice) |
| --- | --- |
| **Firebase Crashlytics** | Free and unlimited, but Android/iOS only (so no Windows) and aimed at Java/Kotlin. .NET exceptions have to be converted and logged by hand through community bindings (`Plugin.Firebase.Crashlytics`), which lag behind .NET and break with trimming/R8 now and then. It also needs a Firebase project, `google-services.json`, and sends data to Google. |
| Sentry | The same SDK and more features; you ruled it out. The DSN swap keeps this door open. |
| Google Play vitals / Microsoft Partner Center | Only for apps installed from the stores; both apps are sideloaded or installed from GitHub. |
| No service | Phases 1–4 plus "Report a problem" already work without one. That is the fallback if nothing is to be sent automatically. |

#### Setup (once, by hand)

- Create a GlitchTip account and an organization, with two projects: `wafe-control-android` and `wafe-control-windows`. The apps are versioned separately (`android-v1.2.0` vs `v1.2.0`), so separate projects keep releases and alerts apart.
- Check where the hosted service stores data. If it isn't the EU and that matters to you, self-host in the EU later.
- Alerts: email on a new issue in either project.
- GitHub secrets `GLITCHTIP_DSN_ANDROID` and `GLITCHTIP_DSN_WINDOWS`.

#### Consent (both apps)

- Add `UserSettings.CrashReports: bool?` in [ISettingsStore.cs](../../src/WafeControl.Core/Services/ISettingsStore.cs); `null` means not asked yet.
- **Ask once** after the first successful sign-in (not in demo mode): "Send anonymous crash reports? They contain the app version, device type and error details; never your email, password or unit serial number." [Send] [Don't send]. WinUI uses a `ContentDialog`; mobile uses `DisplayAlertAsync`.
- **Settings switch** "Send crash reports" (Settings → About on both), with a caption linking to the Privacy section of the README.
- Turning it off applies immediately: `BeforeSend` returns `null` and `SentrySdk.Close()` is called.
- Turning it on takes effect immediately on Windows (`SentrySdk.Init` at runtime) and at the next app start on mobile (the SDK is set up while the app is built). The mobile caption says so.
- A README section "Privacy" lists exactly what is sent, and to whom (GlitchTip).

#### SDK wiring

- **DSN at build time only:** the release workflows pass `-p:CrashReportsDsn=…` from the matching secret, and the csproj turns it into `[AssemblyMetadata("CrashReportsDsn", …)]`. Local and PR builds have no DSN, so nothing is reported from development.
- **Core `Diagnostics/CrashReporting.cs`** (Core references the `Sentry` package):
  - `Configure(SentryOptions o, string platform)`:
    - `Dsn`; `Release = "wafe-control-{platform}@{AppVersion.Current}"` (platform `android` / `windows`).
    - `Environment`: `production`, `beta` (a pre-release suffix in the version) or `dev`.
    - `SendDefaultPii = false`, `AttachStacktrace = true`, `MaxBreadcrumbs = 50`, `AutoSessionTracking = false` (GlitchTip ignores sessions; this saves requests). No tracing or profiling (`TracesSampleRate` unset): they aren't needed and would use up the quota.
    - `SetBeforeSend` / `SetBeforeBreadcrumb` run `LogRedaction` over the message, exception messages and breadcrumb text, and drop the event when consent is off.
    - Tags: `arch`, `language`, `demo`, `unit_model` (from `SystemInfo`, helps spot models that answer differently), and on Windows `install` (`velopack` / `dev`). **Never** send the unit name, serial number, email or Sandcastle key.
  - **Quota guard:** `BeforeSend` also drops an event whose fingerprint was already sent in the last 10 minutes (a small in-memory cache). An error inside the 5 s poll loop is then reported once, not 120 times an hour. This matters most for the Windows tray app, which runs for days.
  - `IsEnabled` / `Enable()` / `Disable()` for the Settings switch.
- **Android:** package `Sentry.Maui`. In `MauiProgram`, `builder.UseSentry(o => CrashReporting.Configure(o, "android"))` only when consent is given and a DSN exists. Logging integration: `MinimumEventLevel = Error`, `MinimumBreadcrumbLevel = Information`, so the last commands and API calls before a crash come along as breadcrumbs.
- **Windows:** package `Sentry.Serilog`.
  - In `App.OnLaunched`, right after the logger is created: when consent is given and a DSN exists, `SentrySdk.Init(o => { CrashReporting.Configure(o, "windows"); o.IsGlobalModeEnabled = true; })`. Global mode fits a single-user desktop app.
  - `AppLogging.CreateLogger` always adds `.WriteTo.Sentry(o => { o.InitializeSdk = false; o.MinimumEventLevel = LogEventLevel.Error; o.MinimumBreadcrumbLevel = LogEventLevel.Information; })`. The sink does nothing while the SDK is off, so the Settings switch works without rebuilding the logger.
  - The SDK hooks `AppDomain.UnhandledException` itself, and `CrashHandler.OnFatal` also logs the same exception as Fatal. The SDK drops the duplicate (it recognizes the same exception instance). Check in the manual test that one crash gives one event.
- **"API changed" signal:** the JSON parse Error from Phase 1 sets the fingerprint `["api-drift", endpoint]`. All users hitting a changed endpoint then group into one issue per endpoint and platform.
- **Crash marker:** `CrashHandler.OnFatal` stores `SentrySdk.LastEventId` in `last-crash.json`, so the ID of the reported crash can be quoted in "Report a problem".

#### CI

- [release-android.yml](../../.github/workflows/release-android.yml) publish step: add `-p:CrashReportsDsn=${{ secrets.GLITCHTIP_DSN_ANDROID }}`.
- [release.yml](../../.github/workflows/release.yml) publish step: add `-p:CrashReportsDsn=${{ secrets.GLITCHTIP_DSN_WINDOWS }}` (both matrix legs; the `arch` tag tells them apart).
- No symbol upload: Windows resolves line numbers on the machine (Phase 4), and Android keeps its PDBs and mapping as an artifact.

#### Tests

- `CrashReportingTests`:
  - the scrubber removes emails from messages, exception messages and breadcrumbs;
  - the consent-off path drops the event;
  - the quota guard drops the repeat within 10 minutes and lets it through after;
  - `Release` and tags come out right for both platforms.
- Manual: the DEBUG crash command in a Release-configured build with test DSNs:
  - on an Android phone;
  - on Windows x64: UI thread, background thread, unobserved task, and switching the setting on and off at runtime.

  Check the stack traces (line numbers on Windows), breadcrumbs, tags, one event per crash, and that the event JSON has no email. Then delete the test issues.

Done when: a crash on a tester's phone or PC shows up in GlitchTip within a minute, with version, device, stack trace and the last 50 breadcrumbs, and with no email in the event.

### Phase 6: Windows native crashes from the event log (S)

Some WinUI crashes never reach .NET: the XAML "stowed exception" fail-fast (`0xc000027b`), access violations in `Microsoft.ui.xaml.dll` or the Windows App SDK, and stack overflows. The process is killed without running any handler from Phase 2, so the log just stops. Windows records them in the Application event log, which the app can read without admin rights.

1. **Package:** `System.Diagnostics.EventLog` (Windows-only; it provides `EventLogReader`).
2. **WinUI `Services/NativeCrashDetector.cs`**, run once on a background thread a few seconds after start:
   - Query the `Application` log with `EventLogReader` and XPath: `*[System[(EventID=1000 or EventID=1026) and TimeCreated[timediff(@SystemTime) <= 604800000]]]` (last 7 days).
   - Keep entries whose event data names `WafeControl.WinUI.exe`:
     - `1000` "Application Error": faulting module, exception code, offset, app version.
     - `1026` ".NET Runtime": a managed crash the hooks missed (e.g. stack overflow), with a stack trace.
   - Skip entries at or before `UserSettings.LastSeenCrashEventTime` (new, Windows only), and entries within ±1 minute of a `last-crash.json` crash already handled by Phase 2. Then save the newest time.
3. **For each new entry:**
   - Log it as `Error` ("Previous session crashed: 0xc000027b in Microsoft.ui.xaml.dll"), so it's in the local log users attach.
   - With consent, send it with `SentrySdk.CaptureMessage(…, SentryLevel.Fatal)` using the fingerprint `["native-crash", module, exceptionCode]`, the raw event fields as extra data, and the version from the event as a tag (it may be older than the running version). The quota guard applies.
   - Show the same "closed unexpectedly" dialog as Phase 2 (once, even if there are several entries).
4. **Tests:**
   - Unit: the parser for 1000/1026 event data, from XML fixtures saved from a real crash (`Fixtures/event-1000.xml`, `event-1026.xml`), and the de-duplication against `LastSeenCrashEventTime` and `last-crash.json`.
   - Manual: force a native crash in a DEBUG build (e.g. `Environment.FailFast` for 1026, or a deliberate stowed exception on a XAML property for 1000), restart, and check the log line, the dialog and the GlitchTip event.

Done when: a forced fail-fast in an installed build appears in the log, the dialog and GlitchTip after the next start, exactly once.

## Order and effort

| # | Phase | Effort | Depends on |
| --- | --- | --- | --- |
| 1 | Logs (mobile), redaction, log level rules | S (½–1 day) | – |
| 2 | Global hooks and crash marker (both apps) | S (½ day) | 1 |
| 3 | `async void` cleanup and analyzer (both apps) | S–M (1 day) | 1 (for logging) |
| 4 | Symbols in releases | S (1–2 h) | – |
| 5 | Crash reports via GlitchTip (both apps) | M (1–1½ days) | 1, 2, 4 |
| 6 | Windows native crashes from the event log | S (½ day) | 2 (5 for sending) |

Do 1–4 before sharing the app, and 5–6 before or right after the first outside users.

## Last step (you): connect GlitchTip

Everything is in place; reports start once the release builds carry a DSN. Nothing is reported until then, and the setting stays hidden.

1. **GitHub secrets** (repository → Settings → Secrets and variables → Actions → New repository secret):
   - `GLITCHTIP_DSN_WINDOWS`: the DSN of the Windows project.
   - `GLITCHTIP_DSN_ANDROID`: the DSN of the Android project.

   With a single GlitchTip project, put the same DSN in both; the reports stay apart by their release name (`wafe-control-windows@…` / `wafe-control-android@…`).
2. **The Security Endpoint isn't needed.** GlitchTip gives it for browsers' Content Security Policy reports (web pages); the apps only use the DSN.
3. **Alerts in GlitchTip:** project → Alerts → an email alert for new issues.
4. **Try it before a release** (optional), with a local build that carries the DSN:
   ```powershell
   dotnet build src/WafeControl.WinUI -p:Platform=x64 "-p:CrashReportsDsn=<your DSN>"
   ```
   Quit the installed 1.1.1 first (tray → Exit): the apps share the single-instance key, so the test build would hand over to it. Run it, answer "Send" to the crash report question (after signing in, or turn it on in Settings), then use Settings → "Demo unit faults (Debug build)" → UI thread. The issue appears in GlitchTip under environment `dev`, with file and line numbers. Start the app again for the "closed unexpectedly" dialog. The "Native (fail fast)" button checks Phase 6: the next start finds it in the event log (the first start of a new install only records where the log is; crash after that).
5. **Release:** tag `v1.2.0` and `android-v1.2.0`; the workflows pass the DSNs to the builds.

## Open questions

- Is GlitchTip's free quota per organization or per project? It only changes how much headroom there is, not the design.
- Should anonymous **usage** analytics (which features are used) ever be added? This plan covers crashes and errors only; usage analytics would need its own consent line.
