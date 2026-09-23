# TODO — Wafe Recuperation App

Last update: 2026-09-24 · SDK 10.0.401 · Windows App SDK 2.5.1

**Goal:** a native **WinUI 3** app for Windows and a **.NET MAUI** app for **Android and iOS**, both on the same UI-agnostic core. The old Avalonia desktop app has been removed.

## Where things stand

| Project | State |
| --- | --- |
| `RecuperationSystem.Shared` | API client (stateless, source-generated JSON, `Content-Length` bodies), `WafeSession` + `SandcastleAuthHandler` (key attach, re-login on 401). |
| `RecuperationSystem.Core` | Services + view models on CommunityToolkit.Mvvm, `AddRecuperationCore()`. Trim/AOT-analyzer clean. |
| `RecuperationSystem.WinUI` | Windows app: login, dashboard, weekly schedule, settings (language, theme, startup, about), tray with live status tooltip, single instance, remembered window placement. Runs against the real API. |
| `RecuperationSystem.Tests` | 165 tests (Shared + Core) on xUnit.net v3 + Microsoft.Testing.Platform, including fixtures captured from the real API. |

`RecuperationSystem.slnx` builds with 0 errors and all tests pass. CI runs on `windows-latest`.

---

## Phase 1: Shared core

- [ ] **Needs real data:** `HeaderInfo`, `SystemInfo`, `Messages` are still unverified. `/main` and `/schedule` are verified. Capture the others the same way (Debug log → fixture), then fix the models or delete them.

## Phase 2: WinUI 3 Windows app

Unpackaged, self-contained Windows App SDK 2.5 (`dotnet run --project src/RecuperationSystem.WinUI`).

- [ ] **Your test pass:** sign out/in and try every control against the unit, including adding, editing and deleting a schedule action. Only the read paths were verified live: no commands or schedule changes were sent.
- [ ] Hide controls for features missing from the unit's `capabilities` list (e.g. `["boost","silent","holiday"]`).
- [ ] Boost countdown that ticks every second between polls.
- [ ] Toast notifications (`AppNotificationManager`): filter health low, unit offline, boost finished.
- [ ] More settings: poll interval.
- [ ] **Auto-updater** (from GitHub Releases, see Phase 7):
  - Title bar, left of minimize/maximize/close: an "Update available" text button, shown only when a newer release exists. Tooltip with the new version.
  - Click → small centered dialog (`ContentDialog`) with the new version and download size (the release asset's `size`), buttons Cancel / Update.
  - Update → download the setup exe for the current architecture (`win-x64`/`win-arm64`) and run it silently (`/VERYSILENT`, plus `/CURRENTUSER` when installed per-user; a Program Files install asks for UAC). Setup closes the running app itself (Restart Manager) and reuses the previous install folder. Relaunch after a silent install: the `[Run]` entry in `installer/WafeRecuperation.iss` is `skipifsilent`, so add a switch for the updater.
  - Check on startup and then every few hours via `GET /repos/viliamb992/wafe-recuperation-app/releases/latest` (skips pre-releases). Compare as SemVer.
- [ ] Jump list (taskbar right-click): Boost 15/30, Stop boost.
- [ ] Schedule: a "now" marker line, copy a day to other days. Actions are added only by selecting in the grid with the mouse (the + button is gone), so keyboard-only users can't add one yet; see the accessibility pass.
- [ ] Accessibility pass (Narrator, keyboard-only, high contrast).

## Phase 4: MAUI mobile app skeleton (Android + iOS)

- [ ] `dotnet new maui -n RecuperationSystem.Mobile` with `TargetFrameworks` = `net10.0-android;net10.0-ios`. Add it to the solution.
- [ ] Choose the `ApplicationId`/bundle id (it can't change after store release), the display name, and minimum OS versions (suggest Android API 26, iOS 15).
- [ ] `MauiProgram`: `AddRecuperationCore()`, platform services, pages. Call `AppViewModel.StartAsync()` on startup.
- [ ] `SecureStorageCredentialStore : ICredentialStore` (Android Keystore / iOS Keychain).
- [ ] App icon + splash (`MauiIcon`, `MauiSplashScreen`) from `app-icon.svg`. Flatten its blur filter first.
- [ ] Shell navigation: Login → Dashboard (+ Settings). Compiled bindings (`x:DataType`) everywhere.
- [ ] Release builds on devices: Android (trimming + R8), iOS.
- [ ] **iOS build host:** needs a Mac (VS "Pair to Mac" or a macOS CI runner). Hot Restart can deploy from Windows for debugging only. An Apple Developer account is needed for TestFlight.

## Phase 5: Mobile dashboard UI

Same information as the Windows dashboard, phone-first. Single column; tablets get two. Light/dark via `AppThemeBinding`.

- [ ] Sensor tiles, power toggle with confirmation for Stop, segmented mode selector, flow slider (send on drag end, haptic tick), boost chips + countdown, Silent/Holiday switches, filter bars.
- [ ] Weekly schedule on `ScheduleViewModel` (same logic as Windows): day-by-day list or scrollable grid, tap/long-press to add, same add/edit sheet.
- [ ] `RefreshView` pull-to-refresh, offline/error banner (`Connectivity`), pending state per control.
- [ ] Use the Core `Strings` for all text, and a language picker in Settings (`ISettingsStore` on MAUI `Preferences`).

## Phase 6: Mobile platform polish

- [ ] Lifecycle: stop polling when backgrounded, refresh on resume.
- [ ] App shortcuts via MAUI `AppActions` (Boost 15/30, Stop boost).
- [ ] Android: edge-to-edge, optional biometric unlock. iOS: safe areas, haptics, optional Face ID.

## Phase 7: CI & release

- [ ] Windows: first real release through `.github/workflows/release.yml` (push a `v*` tag → installer per architecture on a GitHub Release). Built and installed locally, not yet run on GitHub.
- [ ] Windows code signing: the installer and exe are unsigned, so SmartScreen warns on first run (options: Azure Trusted Signing, a code-signing certificate).
- [ ] Android: keystore in secrets, signed `.aab`/`.apk` on tag (runner with `dotnet workload install maui-android`).
- [ ] iOS: certificates + provisioning profile in secrets, `.ipa` → TestFlight (macOS runner).

## Backlog

- [ ] Background notifications on mobile (Android `WorkManager` ≥ 15 min, iOS `BGAppRefreshTask`).
- [ ] Android Quick Settings tile and home-screen widget for boost/status.
- [ ] History chart (temps/CO₂). The API doesn't expose history, so it needs local sampling into SQLite.
- [ ] Messages screen, once that model is verified.

---

## Decisions

- **Windows: native WinUI 3.** Real Fluent controls, Mica, native title bar, tray, Efficiency Mode, single instance. **Android/iOS: .NET MAUI.** Both apps share `Core`; only views and platform services differ.
  - *Considered:* MAUI for all three (one view layer, but weaker Windows polish), and Avalonia everywhere (non-native look on mobile).
- **Core on CommunityToolkit.Mvvm.** ReactiveUI is dropped.
- **WinUI app ships unpackaged + self-contained, with an Inno Setup installer** (`installer/WafeRecuperation.iss`): Program Files or a per-user/custom folder, Start menu entry, uninstaller. Only the app's languages (cs, sk, en) are bundled.
  - *Considered:* MSIX. Cleaner updates and a startup task, but it can't install without a trusted signature (Store or a paid certificate).
- **Windows: polling keeps running while the app is hidden in the tray**, so the tray tooltip always shows the unit's current state.
  - *Considered:* pausing polling while hidden (less API traffic), which would leave the tooltip stale.

## Open questions

- [ ] Distribution for mobile: stores (Play / App Store) or sideload/TestFlight only? This affects bundle ids, signing and the privacy policy.
- [ ] Is a Mac available for iOS builds, or should that go through CI only?
- [ ] How long does a `Sandcastle-Key` session live? Re-login on 401 handles it either way.
- [ ] Any official Wafe API documentation, or are all models reverse-engineered?
