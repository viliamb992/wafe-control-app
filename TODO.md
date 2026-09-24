# TODO — WAFE Control

Last update: 2026-09-24 · SDK 10.0.401 · Windows App SDK 2.5.1 · MAUI 10.0.110

**Goal:** a native **WinUI 3** app for Windows and a **.NET MAUI** app for **Android and iOS**, both on the same UI-agnostic core. The old Avalonia desktop app has been removed.

## Where things stand

| Project | State |
| --- | --- |
| `WafeControl.Shared` | API client (stateless, source-generated JSON, `Content-Length` bodies), `WafeSession` + `SandcastleAuthHandler` (key attach, re-login on 401). |
| `WafeControl.Core` | Services + view models on CommunityToolkit.Mvvm, `AddWafeControlCore()`, display text shared by both apps (`DisplayFormat`). Trim/AOT-analyzer clean. |
| `WafeControl.WinUI` | Windows app: login, dashboard, weekly schedule, settings (language, theme, startup, unit name/rename + service contact, about), unit name in the title bar, tray with live status tooltip, single instance, remembered window placement. Runs against the real API. |
| `WafeControl.Mobile` | Android + iOS app (MAUI): sign-in, Overview/Schedule/Settings tabs, same features as Windows minus the desktop-only ones. Design from [DESIGN.md](DESIGN.md). Android verified on an emulator (API 36) in Debug and Release (full trimming + R8), light and dark, language switch; sign-in reaches the real API. Not yet signed in with a real account. |
| `WafeControl.Tests` | 228 tests (Shared + Core) on xUnit.net v3 + Microsoft.Testing.Platform, including fixtures captured from the real API. |

`WafeControl.slnx` builds with 0 errors and all tests pass. CI runs on `windows-latest` (with the `maui-android` workload).

---

## Phase 1: Shared core

- [ ] **Needs real data:** `/messages`. `/main`, `/schedule`, `/header` and `/info` are verified. No message has shown up yet, so the guessed model was removed; when one does, capture it (Debug log → fixture) and add the model. The header's `message` field (always `null` so far) may be related, and its `pm` flag is still unexplained.

## Phase 2: WinUI 3 Windows app

Unpackaged, self-contained Windows App SDK 2.5 (`dotnet run --project src/WafeControl.WinUI`).

- [ ] **Your test pass:** sign out/in and try every control against the unit, including adding, editing and deleting a schedule action, and renaming the unit (Settings → Unit). Only the read paths were verified live: no commands or schedule changes were sent.
- [ ] Hide controls for features missing from the unit's `capabilities` list (e.g. `["boost","silent","holiday"]`).
- [ ] Boost countdown that ticks every second between polls.
- [ ] Toast notifications (`AppNotificationManager`): filter health low, unit offline, boost finished.
- [ ] More settings: poll interval.
- [ ] **Auto-updater** (from GitHub Releases, see Phase 6):
  - Title bar, left of minimize/maximize/close: an "Update available" text button, shown only when a newer release exists. Tooltip with the new version.
  - Click → small centered dialog (`ContentDialog`) with the new version and download size (the release asset's `size`), buttons Cancel / Update.
  - Update → download the setup exe for the current architecture (`win-x64`/`win-arm64`) and run it silently (`/VERYSILENT`, plus `/CURRENTUSER` when installed per-user; a Program Files install asks for UAC). Setup closes the running app itself (Restart Manager) and reuses the previous install folder. Relaunch after a silent install: the `[Run]` entry in `installer/WafeControl.iss` is `skipifsilent`, so add a switch for the updater.
  - Check on startup and then every few hours via `GET /repos/viliamb992/wafe-recuperation-app/releases/latest` (skips pre-releases). Compare as SemVer.
- [ ] Jump list (taskbar right-click): Boost 15/30, Stop boost.
- [ ] Schedule: a "now" marker line, copy a day to other days. Actions are added only by selecting in the grid with the mouse (the + button is gone), so keyboard-only users can't add one yet; see the accessibility pass.

## Phase 3: MAUI mobile app skeleton (Android + iOS)

Done: `src/WafeControl.Mobile` (`com.wafecontrol.app`, "WAFE Control", Android 8.0 / API 26+, iOS 15+), secure-storage login, icon + splash from the WAFE wordmark, Shell (sign-in → tabs), compiled bindings enforced by the build (XC0022/XC0023/XC0025 are errors), Release build trimmed with R8.

- [ ] **Your test pass on a phone:** sign in with your account, then every control, the schedule (add, edit, delete) and renaming the unit. Only the sign-in failure path reached the real API from the emulator.
- [ ] **iOS:** build, run and check on a Mac (VS "Pair to Mac" or a macOS runner). The `net10.0-ios` target compiles on Windows (`-p:EnableIosBuild=true`) but was never run: check the page-sheet editors, safe areas, the tab bar icons and the input borders. Sideloading on iOS needs a provisioning profile: a free Apple ID (the app expires after 7 days) or a paid developer account (ad hoc, 1 year).
- [ ] Android signing key: create it and add the four `ANDROID_*` secrets (README → Android signing). Keep the keystore: sideloaded updates install only over an APK signed with the same key.

## Phase 4: Mobile dashboard UI

Done: sensor tiles, Start/Stop with confirmation for Stop, segmented mode selector, flow slider (sends on release, haptic tick per 10 m³/h), boost chips with a per-second countdown, Silent/Holiday switches, filter bars, pull-to-refresh, no-internet and unit-offline banners, spinner per pending control, status toast for command results, day-by-day schedule timeline (tap to add/edit, "now" line, add button), add/edit sheet, Settings (language, theme, unit + rename, account, about). Two columns from ~700 dp.

- [ ] Schedule: long-press to copy a day; swipe between days.
- [ ] Screen reader pass (TalkBack, VoiceOver): the timeline's blocks are drawn, so editing an action by screen reader goes through the tap position; consider a list view of the day's actions.
- [ ] Tablet/landscape check on a real tablet.

## Phase 5: Mobile platform polish

- [ ] Lifecycle: stop polling when backgrounded, refresh on resume.
- [ ] App shortcuts via MAUI `AppActions` (Boost 15/30, Stop boost).
- [ ] Android: edge-to-edge, optional biometric unlock. iOS: safe areas, haptics, optional Face ID.

## Phase 6: CI & release

- [ ] Windows: first real release through `.github/workflows/release.yml` (push a `v*` tag → installer per architecture on a GitHub Release). Built and installed locally, not yet run on GitHub.
- [ ] Windows code signing: the installer and exe are unsigned, so SmartScreen warns on first run (options: Azure Trusted Signing, a code-signing certificate).
- [ ] Android: first real release through `.github/workflows/release-android.yml` (push an `android-v*` tag → signed APK on its own GitHub Release, not marked latest). The publish and signing command was tested locally with a throwaway key; the workflow hasn't run on GitHub yet.
- [ ] iOS: certificate + ad hoc provisioning profile in secrets, `.ipa` for sideloading (macOS runner).

## Backlog

- [ ] Background notifications on mobile (Android `WorkManager` ≥ 15 min, iOS `BGAppRefreshTask`).
- [ ] Android Quick Settings tile and home-screen widget for boost/status.
- [ ] History chart (temps/CO₂). The API doesn't expose history, so it needs local sampling into SQLite.
- [ ] Messages screen, once a real `/messages` response has been captured.

---

## Decisions

- **Windows: native WinUI 3.** Real Fluent controls, Mica, native title bar, tray, Efficiency Mode, single instance. **Android/iOS: .NET MAUI.** Both apps share `Core`; only views and platform services differ.
  - *Considered:* MAUI for all three (one view layer, but weaker Windows polish), and Avalonia everywhere (non-native look on mobile).
- **Core on CommunityToolkit.Mvvm.** ReactiveUI is dropped.
- **WinUI app ships unpackaged + self-contained, with an Inno Setup installer** (`installer/WafeControl.iss`): Program Files or a per-user/custom folder, Start menu entry, uninstaller. Only the app's languages (cs, sk, en) are bundled.
  - *Considered:* MSIX. Cleaner updates and a startup task, but it can't install without a trusted signature (Store or a paid certificate).
- **Windows: polling keeps running while the app is hidden in the tray**, so the tray tooltip always shows the unit's current state.
  - *Considered:* pausing polling while hidden (less API traffic), which would leave the tooltip stale.
- **One design manual for both apps: [DESIGN.md](DESIGN.md).** The mobile app re-creates the WinUI (Fluent 2) look with the same tokens, a brand-blue accent instead of the Windows accent color, slightly larger radii for touch, and Fluent System Icons (a 38-glyph subset font, `tools/subset-icons.py`).
  - *Considered:* Material on Android and native iOS styling (more "native" per platform, but three looks for one product), and CommunityToolkit.Maui (not needed for what the app uses).
- **Mobile apps are sideloaded, not published to Play or the App Store.** App id `com.wafecontrol.app`; no store listing, review or privacy policy needed.
  - *Considered:* store releases (automatic updates, but developer accounts, review and a privacy policy for a personal tool).
- **Mobile navigation: bottom tabs** (Overview, Schedule, Settings) instead of Windows' single window with Back.

## Open questions

- [ ] Is a Mac available for iOS builds, or should that go through CI only?
- [ ] How long does a `Sandcastle-Key` session live? Re-login on 401 handles it either way.
- [ ] Any official Wafe API documentation, or are all models reverse-engineered?
