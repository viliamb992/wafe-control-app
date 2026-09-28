# Plan: auto-updater and UX improvements

Status: **implemented** on branch `crash-tracking-ux` (2026-09-28). Revised before implementation: no Sentry; Report a problem works without any paid service.

Differences from the plan below:

- **Updater:** Velopack, as recommended; the Inno Setup script is gone. The install moves to `%LocalAppData%\WafeControl.App` (packId `WafeControl.App`), so uninstall 1.1.x before installing 1.2.0 (see README → Release). Tested: the release workflow's `vpk pack` builds the Setup, packages and feed. Not tested yet: installing and updating between two versions (see below).
- **Sign-in help:** go2my.wafe.eu has no password reset (only "change password" with the old one), so instead of a "Forgot password?" link the form says: "Forgot your password or have no account? Your installer or the Wafe service can help." with a link to wafe.eu.
- **Demo mode on Windows:** one banner in the main window, above whichever screen is open, instead of one per screen. On Android: Overview and Schedule banners and the account row.
- **Demo faults** are in Windows Debug builds only (Settings → "Demo unit faults").
- **Command confirmation, simplified after review:** no "Waiting for the unit…" step and no "hasn't confirmed yet" warning (too much clutter). The "sending" text stays until the result; when the unit doesn't confirm in time it just goes away, and a late confirmation shows the normal success message.
- **"Updated to 1.2.0"** shows in the footer after an update, without a "What's new" link; the notes are in the update flyout before the update and on the GitHub release.

Not verified on a device yet (nothing here could run with your installed 1.1.1 in the tray, and there is no phone attached):

- Windows: sign in, demo mode, a command's feedback and Retry, the stale-data banner (demo fault StaleHeader), Report a problem, the crash dialog.
- Updater: `vpk pack` two versions (e.g. 1.2.0-test.1 and 1.2.0-test.2) into one folder, install the first with its Setup.exe, start it with `WAFE_UPDATE_SOURCE=<folder>`, then check the download, "Restart to update" from the window and from the tray, and apply on Exit. Quit the installed 1.1.1 first: the apps share the single-instance key, so a second one hands over to it.
- Android: the same, plus Share logs and the crash-report question. Crash reporting and logs are in [crash-tracking.md](crash-tracking.md); "Report a problem" builds on them.

Contents:

1. [Silent auto-updater (Windows)](#1-silent-auto-updater-windows)
2. [Groundwork: API results and command feedback](#2-groundwork-api-results-and-command-feedback)
3. [Error messages](#3-error-messages)
4. [Command confirmation](#4-command-confirmation)
5. [Stale data](#5-stale-data)
6. [Sign-in help](#6-sign-in-help)
7. [Demo mode](#7-demo-mode)
8. [Report a problem](#8-report-a-problem)
9. [Order and effort](#9-order-and-effort)

---

## 1. Silent auto-updater (Windows)

### What the user sees

1. The app checks GitHub 30 s after start and then every 4 hours.
2. When a new version exists, it downloads and prepares it **in the background**. The title bar shows a small, quiet "Downloading update 40 %" indicator; hovering shows the version.
3. When the update is ready, a title bar button **"Restart to update"** (accent style) appears, also as a tray menu item. Clicking it opens a flyout with the version, release notes and [Restart now]; the restart takes about a second and the app comes back in the same state (window or tray).
4. If the user never restarts, the update applies when the app exits (tray → Exit, or Windows shutdown), so the next launch is the new version.
5. After updating, a one-time message says "Updated to 1.3.0 · What's new".
6. Settings → About gets "Updates": **Download updates automatically** (on by default; when off, the button says **"Update"** and downloads on click, then turns into "Restart to update"), **Get beta versions** (pre-release tags), and **Check now**.

### Technical choice: Velopack instead of Inno Setup (recommended)

"Installed in the background, then Restart to apply" can't be done with the current Inno Setup installer. An in-place installer has to overwrite the exe and DLLs, which are locked while the app runs, so the install can only happen after the app exits. On top of that, the current default install goes to Program Files (`PrivilegesRequired=admin` in [WafeControl.iss](../../installer/WafeControl.iss)), so every silent update would trigger a UAC prompt.

[Velopack](https://velopack.io) is built for exactly this. It installs per user to `%LocalAppData%\<packId>\current\`, downloads the full or **delta** package in the background, prepares it next to the running app, and swaps `current` in about a second on restart or exit. It reads updates straight from GitHub Releases, needs no admin rights, and supports code signing (including Azure Trusted Signing) when that comes.

What changes compared with Inno:

| | Inno Setup (today) | Velopack |
| --- | --- | --- |
| Install location | Program Files (default) or per user | Per user only (`%LocalAppData%\WafeControl.App`) |
| Setup UI | Localized wizard, desktop icon choice | One-click setup with a splash screen, not localized |
| Update | Manual download | Background, delta, "Restart to update" |
| Admin rights | Default yes | Never |
| Uninstall | Apps & features | Apps & features (Velopack registers it) |

Because the app hasn't been shared yet, the only Inno installs to migrate are your own, so **now is the cheapest time to switch**.

The alternative that keeps Inno is described at the end of this section.

### Implementation (Velopack)

**App side**

1. **Package:** add `Velopack` to Directory.Packages.props and the WinUI project.
2. **[Program.cs](../../src/WafeControl.WinUI/Program.cs):**
   - Call `VelopackApp.Build().OnBeforeUninstallFastCallback(v => RegistryStartupRegistration.RemoveEntries()).Run();` as the **first line** of `Main`, before `WinRT.ComWrappersSupport.InitializeComWrappers()` and the single-instance check. Install, update and uninstall hooks then run and exit quickly.
   - The uninstall hook replaces the Inno `[Registry] uninsdeletevalue` lines. `RemoveEntries()` is a new static method that deletes the `Run` and `StartupApproved` values (the current one and the legacy `WafeRecuperation`).
3. **Core: `Services/IUpdateService.cs`**, so the view model stays platform-free and testable:
   ```csharp
   public enum UpdateStage { None, Available, Downloading, ReadyToRestart, Failed }
   public interface IUpdateService
   {
       bool IsSupported { get; }                 // false for dotnet run / portable builds
       UpdateStage Stage { get; }
       string? AvailableVersion { get; }
       int DownloadProgress { get; }             // 0–100
       string? ReleaseNotesMarkdown { get; }
       event EventHandler? Changed;              // may be raised off the UI thread
       Task CheckAsync(CancellationToken ct);    // checks, and downloads when AutoDownload
       Task DownloadAsync(CancellationToken ct);
       void RestartToApply();                    // does not return
       void ApplyOnExit();                       // call from the Exit path
   }
   ```
   Add `UserSettings.AutoDownloadUpdates` (default `true`), `UserSettings.BetaUpdates` (default `false`) and `UserSettings.LastRunVersion` (for the "Updated to …" message).
4. **Core: `ViewModels/UpdateViewModel.cs`** has `IsVisible`, `ButtonText` ("Update" / "Downloading 40 %" / "Restart to update"), `IsAccent`, `IsBusy`, `VersionText`, `ReleaseNotes`, `CheckNowCommand`, `PrimaryCommand` (download or restart, depending on the stage), and `StatusText` for Settings ("Up to date · checked 10:42", "Couldn't check for updates").
   - The schedule is a `PeriodicTimer` (30 s delay, then every 4 h), using `TimeProvider` for tests.
   - Failures are logged as Warning and retried at the next tick. No error dialog: a failed update check isn't the user's problem.
5. **WinUI: `Services/VelopackUpdateService.cs`**:
   - `new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: settings.BetaUpdates))`.
   - `IsSupported = manager.IsInstalled`.
   - `CheckForUpdatesAsync()` → `DownloadUpdatesAsync(info, progress => …)` → `Stage = ReadyToRestart`.
   - On start, `manager.UpdatePendingRestart` is set when an update was downloaded earlier but not applied; start in `ReadyToRestart`.
   - `RestartToApply()` calls `MainWindow.PrepareExit()` (saves placement, disposes the tray icon so no ghost icon stays behind), then `Log.CloseAndFlush()`, then `manager.ApplyUpdatesAndRestart(pending, restartArgs)`. Pass `--tray` when the window is hidden, so the app comes back in the tray.
   - `ApplyOnExit()` calls `manager.WaitExitThenApplyUpdates(pending, silent: true, restart: false)`.
   - Development override: the env var `WAFE_UPDATE_SOURCE=<local folder>` → `new UpdateManager(folder)`, for testing against packages built locally.
6. **WinUI UI:**
   - [MainWindow.xaml](../../src/WafeControl.WinUI/MainWindow.xaml): a button in the `TitleBar.RightHeader` (or the footer next to the version) bound to `UpdateViewModel`, with a `Flyout` showing the version, release notes (plain text is fine at first) and [Restart now].
   - Tray menu: "Restart to update" item, visible only when ready.
   - Tray tooltip: adds "Update ready" as a line.
   - Settings → About: the three settings, Check now and `StatusText`.
   - `App.OnLaunched`: if `LastRunVersion` differs from `AppVersion.Current` and isn't null, show "Updated to {0}" in the footer status with a "What's new" link to the GitHub release; then save the new version.
7. **Launch arguments:** accept `--tray`, which behaves like `StartInTray` for this launch only.
8. **Startup entry:** `RegistryStartupRegistration.Command` uses `Environment.ProcessPath`, which is the stable `…\current\WafeControl.WinUI.exe` under Velopack, so the entry survives updates. Remove the now-unused `LegacyInstallMigration` in a later release, once no 1.0.x installs remain.
9. **Strings** (cs/sk/en): `UpdateDownloading`, `UpdateRestart`, `UpdateAvailable`, `UpdateReadyTitle`, `UpdateRestartNow`, `UpdateUpToDate`, `UpdateCheckFailed`, `UpdateCheckNow`, `UpdateAutoDownload`, `UpdateBeta`, `UpdatedTo`, `UpdateWhatsNew`, `TrayUpdateReady`.

**Release side** ([release.yml](../../.github/workflows/release.yml))

1. `dotnet tool install -g vpk` (pin the version).
2. Per matrix leg (`win-x64`, `win-arm64`), after `dotnet publish`:
   ```powershell
   vpk download github --repoUrl $repo --channel win-x64 -o releases    # previous release, for the delta
   vpk pack --packId WafeControl.App --packVersion $version --packDir publish `
     --mainExe WafeControl.WinUI.exe --packTitle "WAFE Control" --packAuthors "Viliam Birmon" `
     --icon src/WafeControl.WinUI/Assets/app-icon.ico --channel win-x64 `
     --shortcuts StartMenuRoot --releaseNotes release-notes.md -o releases
   ```
   Upload `releases/*` as the artifact.
3. The `release` job creates the GitHub release as now and uploads **all** files from both legs. Their names don't collide: `releases.win-x64.json`, `WafeControl.App-1.2.0-win-x64-full.nupkg`, `…-delta.nupkg`, `WafeControl.App-win-x64-Setup.exe`, `…-Portable.zip`.
4. Pre-release tags (`v1.3.0-beta.1`) are published with `--prerelease`, and the app only sees them with "Get beta versions" on.
5. Android releases in the same repository carry no `releases.win-*.json`, so the Windows updater ignores them. The rule "Windows release is latest" stays harmless.
6. **Release notes:** add `CHANGELOG.md` (a "## 1.3.0" section per version). The workflow extracts the section for the tag into `release-notes.md` for `vpk pack` and `gh release create --notes-file`.
7. Delete [installer/WafeControl.iss](../../installer/WafeControl.iss) and the Inno Setup step. Update the README sections Release, Where data is stored and Tools, and the latest-release table (the asset is now `WafeControl.App-win-x64-Setup.exe`).

Check the flag names against the current `vpk` docs when implementing; they have changed between versions.

**Migration of existing (Inno) installs:** uninstall 1.1.x from Apps & features, then run the new Setup. The data in `%AppData%\WafeControl` (settings, remembered login) and the logs in `%LocalAppData%\WafeControl\logs` are untouched, because the new install goes to `%LocalAppData%\WafeControl.App`. Mention this in the first Velopack release's notes.

**Tests and checks**

- `UpdateViewModelTests` (fake `IUpdateService` and `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`):
  - the stage → button text/visibility mapping;
  - auto-download off keeps the stage at `Available`;
  - a failed check doesn't throw and retries at the next tick;
  - "Updated to" shows once.
- Manual, with `WAFE_UPDATE_SOURCE`:
  - pack 1.3.0-test1 and 1.3.0-test2 locally, install test1, and check the download, the button, restart from the window and from the tray, apply on Exit, and that the startup entry still works;
  - run the whole path on an ARM64 machine or VM once.

### Alternative: keep Inno Setup

- `GET https://api.github.com/repos/viliamb992/wafe-control-app/releases/latest` gives the version and the asset `WafeControl-{v}-win-{arch}-setup.exe`. Download it to `%LocalAppData%\WafeControl\updates` and check it against the asset's `digest` (`sha256:…`).
- "Restart to update" exits the app and runs `setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1`. Add a `[Run]` entry with `Check: ShouldRelaunch` that reads `{param:RELAUNCH}`, because the current entry is `skipifsilent`.
- The default install mode has to change to per user (`PrivilegesRequired=lowest`, `DefaultDirName={localappdata}\Programs\WAFE Control`), or updates on Program Files installs ask for UAC.
- Downside: the install runs after the click, so there are 5–20 seconds with no app, no deltas, and more code to own (download, verification, relaunch) than Velopack's four calls.

---

## 2. Groundwork: API results and command feedback

Sections 3–5 need two things the code doesn't have yet: **why** a call failed, and a way to show command results that isn't a plain string.

### `ApiResult`

`WafeApiService` returns `null` or `false` on any failure today, so the UI can't tell "no internet" from "wrong password" from "Wafe server down".

- Shared `Services/ApiResult.cs`:
  ```csharp
  public enum ApiError { None, Offline, Timeout, Unauthorized, Rejected, ServerError, InvalidResponse, Unexpected }
  public readonly record struct ApiResult(ApiError Error, int? StatusCode = null) { public bool Ok => Error == ApiError.None; }
  public readonly record struct ApiResult<T>(T? Value, ApiError Error, int? StatusCode = null) where T : class { … }
  ```
- One classifier, `ApiErrors.From(Exception)` and `ApiErrors.From(HttpStatusCode)`:
  - `HttpRequestException` with a `SocketException` inner exception, or DNS failure → `Offline`;
  - Polly `TimeoutRejectedException` / `TaskCanceledException` not caused by the caller's token → `Timeout`;
  - 401/403 after a failed renewal → `Unauthorized`;
  - other 4xx → `Rejected`; 5xx → `ServerError`; `JsonException` → `InvalidResponse`.
- `IWafeApiService` returns `ApiResult` / `ApiResult<T>`, and `AuthenticateAsync` returns `ApiResult` (401/403 or no key → `Unauthorized`). Update `WafeApiServiceTests` with a test per classification.
- `ISystemControlService` command methods return `CommandOutcome`:
  ```csharp
  public enum CommandStatus { Confirmed, Pending, Failed }
  public sealed record CommandOutcome(CommandStatus Status, ApiError Error = ApiError.None);
  ```
- The refresh path records `LastRefreshError` and `ConsecutiveRefreshFailures` for section 5.

### `Feedback` instead of `StatusMessage` for commands

`AppViewModel.StatusMessage` mixes connection state ("Unit online") with command results ("Boost on"). [StatusToast.IsNews](../../src/WafeControl.Mobile/Controls/StatusToast.cs) has to tell them apart by comparing localized strings.

- Core `ViewModels/Feedback.cs`:
  ```csharp
  public enum FeedbackKind { Progress, Success, Warning, Error }
  public sealed record Feedback(string Text, FeedbackKind Kind, IAsyncRelayCommand? Retry = null, bool OffersReport = false);
  ```
- `AppViewModel.Feedback` (observable) carries command results. `StatusMessage` keeps only the connection state (later replaced by `DataState`, section 5).
- The cards set `App.Feedback = …` instead of `App.StatusMessage = …`. `IAppContext` gets the property.
- WinUI footer: an icon for the kind, the text, and a "Retry" hyperlink button when `Retry` is set. Success clears itself after 5 s.
- Mobile `StatusToast`: subscribes to `Feedback`; the `IsNews` string comparison goes away. When `Retry` is set, it gets an action button (drop `InputTransparent` while shown) and stays 8 s.

---

## 3. Error messages

The problem: `string.Format(Strings.FlowError, ex.Message)` and similar strings (`ModeError`, `BoostError`, `SilentError`, `HolidayError`, `SystemToggleError`, `AppStatusRefreshError`) put raw .NET exception text, usually English, into a Czech UI. Rejections and timeouts all look the same.

1. **Localized reasons** in cs/sk/en, with one `Core/Localization/ErrorText.For(ApiError)`:

   | ApiError | English text |
   | --- | --- |
   | Offline | No internet connection. |
   | Timeout | The Wafe server didn't answer in time. Try again. |
   | Unauthorized | Your sign-in has expired. Sign in again. |
   | Rejected | The Wafe server refused the change. |
   | ServerError | The Wafe server has a problem right now. Try again later. |
   | InvalidResponse | The Wafe server answered in an unexpected way. The app may need an update. |
   | Unexpected | Something went wrong. If it keeps happening, report a problem. |
2. **Per-command messages** use the reason instead of `ex.Message`. For example, `ModeError` becomes `"Couldn't change the mode. {0}"` with `{0} = ErrorText.For(error)`. Keep the `{0}` count equal in all three languages (the existing `StringsTests` checks it). The old `…Error` keys get new values; no new keys are needed per command.
3. **Unexpected exceptions** in the cards' `catch (Exception)` get `ApiError.Unexpected`, log Error (which reaches GlitchTip when crash reports are on), and set `Feedback(…, OffersReport: true)`, which shows a "Report" link (section 8).
4. **Schedule** load/save errors ([ScheduleViewModel.cs](../../src/WafeControl.Core/ViewModels/Schedule/ScheduleViewModel.cs)) and unit rename use the same `ErrorText`.
5. **`Unauthorized`** from any command returns to the sign-in screen with `Login.ErrorMessage = ErrorText.For(Unauthorized)`.
6. **Tests:**
   - `ErrorTextTests`: every `ApiError` has a text in all languages;
   - card tests: a failed `CommandOutcome` gives the expected localized feedback, and no feedback text contains an exception message.

---

## 4. Command confirmation

Today [SystemControlService.SendAndConfirmAsync](../../src/WafeControl.Core/Services/SystemControlService.cs) sends a PUT, then polls `/main` every 3 s for up to 30 s until the value and `gen` change. The user sees "…" and then either success or "not confirmed". A slow unit and a failed command look alike, and there is no retry.

1. **Two visible phases:**
   - "Sending…" until the PUT succeeds;
   - then "Waiting for the unit…" (`Feedback` Progress) until confirmed.

   A failed PUT is shown at once as an error (section 3) instead of after 30 s.
2. **Faster first polls:** intervals of 1 s, 1 s, 2 s, 2 s, then 3 s, total timeout unchanged (`PollingConfiguration.StateChangeIntervalsMs` as an array). The logs already record "confirmed after X ms (N polls)": check a week of your own logs before picking the numbers.
3. **Show the pending value right away:**
   - the control shows the requested state at once with a pending style (the mode segment gets a small spinner, the switch is disabled while pending, the flow value shows "→ 120 m³/h");
   - on `Failed` it goes back to the last confirmed state.

   Cards get `PendingValue` / `IsPending` (the flow card's `IsChanging` becomes the model for the others).
4. **"Not confirmed" is not "failed":**
   - after the timeout the outcome is `Pending`, and the message is a warning: "The unit hasn't confirmed the change yet. It may still apply." with [Retry];
   - the regular 5 s poll keeps watching that value for 2 more minutes. If it arrives: "Confirmed (took longer than usual)". If the unit clearly shows the old value by then, the pending state goes back as in step 3.
5. **Retry** sends the same command again through `Feedback.Retry`, on both platforms.
6. **Stop confirmation on Windows:** mobile asks before stopping the unit and Windows doesn't. Add a `ContentDialog` with the same strings (`SystemStopConfirmTitle`, `SystemStopConfirmMessage`).
7. **Tests** (`SystemControlServiceTests`, `FakeTimeProvider`):
   - PUT rejected → `Failed` immediately without polling;
   - confirmed on the 2nd poll;
   - timeout → `Pending`;
   - late confirmation raises the "took longer" feedback;
   - a pending value goes back after `Failed`.

---

## 5. Stale data

The dashboard shows the last known values with nothing to warn when they're old. Failed polls keep the last status on purpose ([SystemControlService.RefreshAsync](../../src/WafeControl.Core/Services/SystemControlService.cs)), but the UI doesn't say so. `LastUpdate` (the unit's own timestamp from `/header`) is only in a tooltip on Windows and a caption on mobile.

1. **One state** in `AppViewModel`:
   ```csharp
   public enum DataState { Live, Stale, UnitOffline, ServerUnreachable, NoInternet, Demo }
   ```
   - `NoInternet`: the device reports no network (mobile `IConnectivity`; Windows `NetworkInformation.GetInternetConnectionProfile()` + `NetworkStatusChanged`, behind a new `INetworkStatus` in Core).
   - `ServerUnreachable`: 3 or more consecutive failed refreshes (about 15 s).
   - `UnitOffline`: `/header` says `online: false` (existing).
   - `Stale`: online, but `LastUpdate` older than `StaleAfter` (start at 3 min; check typical header timestamp gaps in the logs first).
   - `Live`: everything fine.

   It is recomputed after every poll attempt (success or failure) and by a 30 s timer for the "x min ago" text. `TimeProvider` is injected.
2. **Texts** (cs/sk/en):
   - "Can't reach the Wafe server. Showing data from 14:02."
   - "Data may be out of date: last update 6 min ago."
   - "No internet connection. Showing data from 14:02."
   - The existing offline banner texts for `UnitOffline`.
3. **UI:**
   - **WinUI:** an `InfoBar` (Warning) at the top of `DashboardView` for Stale, ServerUnreachable and NoInternet, with a [Refresh] button. The footer dot turns green / amber / red with a short text ("Live", "Data 6 min old", "No connection"). Tiles are dimmed (`Opacity 0.6`) while not `Live`. The tray tooltip gets the same short text.
   - **Mobile:** reuse `InfoBanner` (like `NoInternetBanner`) with the same texts. The caption "Updated 14:02" turns amber, and the tiles are dimmed.
   - Commands stay enabled when `Stale` but are disabled when `NoInternet` or `ServerUnreachable` (the banner explains why), so the user doesn't wait for a command that can't work.
4. **Poll back-off** while `ServerUnreachable`: 5 s → 10 s → 30 s → 60 s, and back to 5 s after a success or a manual refresh. This saves battery and log noise.
5. **Tests** (`AppViewModelTests`, fake time and fake services): the state for each condition, the transitions back to `Live`, and the back-off sequence.

---

## 6. Sign-in help

Today the sign-in screen shows only "Use your Wafe account (go2my.wafe.eu)." and "Sign in failed." for every failure.

1. **Clearer description:** "Sign in with the email and password you use for the Wafe web app (go2my.wafe.eu)."
2. **Links under the form:**
   - "Forgot password?" opens the portal's password reset. Find the actual URL on go2my.wafe.eu; fall back to the portal home page.
   - "No account? Your Wafe installer or dealer sets it up." as plain help text with a link to the portal.
   - **"Try the demo"** (section 7).
3. **Specific failures** through `ApiResult`: wrong email or password (`Unauthorized`), no internet, server unavailable, timeout. Each gets its own `ErrorText`, instead of the one `LoginFailed`.
4. **Input details:**
   - trim spaces around the email before sending (a pasted address often has one);
   - Windows: a "Caps Lock is on" hint under the `PasswordBox` (check the key state on focus and KeyDown);
   - mobile: a show-password eye button on the password `Entry`;
   - "Keep me signed in" gets a caption: "Stored encrypted on this device only."
5. **Footer line:** "Unofficial app · not affiliated with WAFE s.r.o." with a link to the README. It builds trust and matches the license notice.
6. **Tests:** `AppViewModel` sign-in maps each `ApiError` to the right message, and the email is trimmed.

---

## 7. Demo mode

Lets people try the app without a Wafe unit (friends deciding whether to buy one, or you showing it off). It is also a **development tool**: it can simulate slow, offline and rejecting units to test sections 3–5 without touching the real ventilation, and it makes screenshots without exposing your unit.

### Simulated unit

Core `Demo/DemoWafeApi.cs : IWafeApiService`, in memory, driven by `TimeProvider`:

- **`/main`:**
  - stop/running, mode, requested flow;
  - actual flow moving toward the requested flow over about 10 s;
  - boost counting down, silent/holiday;
  - temperatures following a daily curve (outdoor 4–14 °C, supply, indoor about 22 °C, exhaust);
  - CO₂ rising in the evening and falling with higher flow;
  - humidity about 45 %, filters 72 % / 80 %;
  - `gen` goes up on every change and every 10 s.
- **Commands** apply after 1.5–3 s, so the "Waiting for the unit…" path is exercised.
- **`/header`:** name "Demo", online, timestamp now. **`/info`:** model "Demo unit", serial `DEMO-0001`, service contact `service@example.com` / `https://example.com`. Never use real Wafe contacts or names.
- **Schedule:** a sample week (weekday boost 7:00–7:30 and 18:00–19:00, night minimum), kept in memory; edits work until sign-out.
- **Fault injection (DEBUG builds only):** `DemoFaults { SlowConfirm, NeverConfirm, RejectCommands, ServerDown, UnitOffline, StaleHeader }`, toggled from a Debug section in Settings.

### Wiring

1. `WafeSession` gets `IsDemo` and `StartDemo()`; `Clear()` resets it.
2. Register the real typed client as `AddHttpClient<WafeApiService>()`. Register `IWafeApiService` as a singleton `DemoAwareWafeApi(WafeApiService real, DemoWafeApi demo, WafeSession session)` that routes every call by `session.IsDemo`. `AuthenticationService` and `SystemControlService` stay unchanged.
3. `IAuthenticationService.StartDemo()` sets `IsAuthenticated = true` without touching the credential store. `IsDemo` is exposed. Auto-login never restores demo mode. Logout leaves demo.
4. `AppViewModel`: `IsDemo`, `TryDemoCommand` (sign-in screen), `ExitDemoCommand` (= sign out, then the sign-in form focused on email). `DataState.Demo` replaces the online indicator.
5. The logs and crash reports carry `demo=true`; the crash report consent prompt isn't shown in demo mode.

### Making demo mode obvious

- **WinUI:**
  - a non-closable `InfoBar` (Informational) at the top of Dashboard, Schedule and Settings: **"Demo mode: this is a simulated unit, nothing is sent to a real device."** with a **[Sign in with your account]** button;
  - title bar subtitle "Demo";
  - footer dot shows "Demo" (blue) instead of Online;
  - tray tooltip first line "WAFE Control · Demo";
  - the ⋯ menu's "Sign out" becomes "Leave demo".
- **Mobile:** the same banner (an `InfoBanner` with an action button) at the top of Overview and Schedule. The Settings account card shows "Demo mode" and a [Sign in with your account] button instead of Sign out.
- **Strings:** `DemoBannerTitle`, `DemoBannerMessage`, `DemoSignIn`, `DemoTry`, `DemoLeave`, `DemoSubtitle`.

### Tests

- `DemoWafeApiTests`: commands confirm through the real `SystemControlService` within the timeout (`FakeTimeProvider`), boost counts down, schedule round-trips, faults produce the expected `ApiError`.
- `AuthenticationServiceTests`: demo never saves credentials and auto-login doesn't restore it.
- Manual: take the screenshots in [docs/SCREENSHOTS.md](../SCREENSHOTS.md) from demo mode from now on.

---

## 8. Report a problem

1. **Entry points:**
   - Windows: ⋯ menu and Settings → About;
   - mobile: Settings → About;
   - the crash dialog on the next start ([crash-tracking.md](crash-tracking.md), Phase 2);
   - "Report" links on unexpected errors (section 3).
2. **Diagnostics** (`Core/Diagnostics/DiagnosticInfo.cs`): app version, platform and architecture, OS version, device model, language, demo on/off, install type (Velopack/dev), last crash time and crash report id (when crash reports are on), unit model. **Never** the email, unit name or serial number. Tested for the absence of those.

   Crash reports ([crash-tracking.md](crash-tracking.md)) are opt-in, so for users who said no, this is the only way you learn about problems. For the others, the crash report id links their description to the GlitchTip event.
3. **GitHub issue (both apps):**
   - Add `.github/ISSUE_TEMPLATE/bug.yml` (an issue form with fields `app-version`, `platform`, `os`, `what-happened`, `steps`, `crash-id`) and `config.yml` (blank issues off, a link to the README).
   - The button opens `https://github.com/viliamb992/wafe-control-app/issues/new?template=bug.yml&app-version=…&platform=…&os=…&crash-id=…`. Issue forms fill fields from query parameters with the same `id`; check this with the real form.
   - Logs can't be attached through a URL. Windows opens the log folder next to it with the hint "Drag the newest log file into the issue".
4. **Without a GitHub account** (most people you share with):
   - Windows: "Copy diagnostics" puts the diagnostics text on the clipboard. The dialog suggests sending it with the log file by email (to the address from the open question below) or any other channel.
   - Android: "Send by email / share" opens the share sheet with the redacted logs zip ([crash-tracking.md](crash-tracking.md), Phase 1) and the diagnostics text as the message. The user picks email, WhatsApp, Drive and so on. It's free, works without any service, and works even when crash reports are turned off.
5. **Strings:** `ReportProblem`, `ReportDialogTitle`, `ReportDialogMessage`, `ReportOpenGitHub`, `ReportShare`, `ReportCopyDiagnostics`, `ReportCopied`, `ReportLogsHint`.

---

## 9. Order and effort

| # | Item | Effort | Depends on |
| --- | --- | --- | --- |
| 1 | Auto-updater (Velopack), CHANGELOG, workflow | M (2–3 days incl. testing) | – (do before sharing the app) |
| 2 | Groundwork: `ApiResult`, `CommandOutcome`, `Feedback` | M (1–2 days, mostly tests) | – |
| 3 | Demo mode incl. fault injection | M (1–2 days) | 2 |
| 4 | Error messages | S (½ day) | 2 |
| 5 | Stale data | S–M (1 day) | 2 |
| 6 | Command confirmation | M (1 day) | 2, 4 |
| 7 | Sign-in help | S (½ day) | 2, 3 (demo link) |
| 8 | Report a problem (GitHub issue, share/copy diagnostics) | S (½–1 day) | crash-tracking Phase 1–2 |

Demo mode comes early on purpose: its fault injection is how sections 4–6 get tested without switching the real unit on and off.

Mobile gets sections 2–8 through the shared Core. The Android equivalent of the updater (a "New version available" banner that links the APK from the latest `android-v*` release) is a small follow-up and isn't covered here.

## Open questions

- **Velopack vs Inno:** this plan recommends Velopack (true background install). Keeping Inno is possible (see the end of section 1), but the install then happens during the restart.
- **Report channel:** which email address (if any) should users without GitHub send reports to? It would appear in the app and the README.
- **Password reset URL** on go2my.wafe.eu: find it before section 6.
- **Stale threshold and confirmation poll timings:** check against a week of logs before fixing the numbers.
