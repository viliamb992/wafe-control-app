# WAFE Control

Unofficial apps for monitoring and controlling a Wafe heat-recovery ventilation unit through the go2my.wafe.eu cloud API.

## Apps

| Project | Platform | Status |
| --- | --- | --- |
| `WafeControl.WinUI` | Windows 11, native WinUI 3 | Main desktop app |
| Android / iOS | .NET MAUI | Planned, see [TODO.md](TODO.md) |

### Windows app features

- Sign in with your Wafe account. "Keep me signed in" remembers the login, and the app signs in again automatically when the session expires.
- Dashboard: running state with Start/Stop, outdoor/supply/indoor/exhaust temperatures, CO₂ with air-quality hint, humidity (only on units with that sensor), operating mode (Intelligent / Manual / Schedule), flow rate (Manual mode), Boost 15/30/60 min, Silent and Holiday modes, filter health.
- Windows 11 look: Mica, native title bar, light/dark theme (follows Windows or set in Settings), layout adapts to window width. The window reopens where you left it; the version is shown in the footer and in Settings → About.
- Weekly schedule: a Mon–Sun grid like the Wafe web app. Click or drag to add an action, click a block to edit or delete it (up to 50 actions).
- Closing the window keeps the app in the tray (in Efficiency Mode), or exits it if you choose that in Settings. Pointing at the tray icon shows whether the unit is running, its mode, air flow and CO₂. The tray menu offers boost shortcuts and Exit. Launching the app again brings the existing window back (single instance).
- Optional: start with Windows, and start hidden in the tray (the window still opens if you need to sign in).
- Czech (default), Slovak and English. Switch in Settings (gear in the footer, also on the sign-in screen). The change applies right away and is kept for the next launch.

## Translations

All app text lives in `src/WafeControl.Core/Localization`: `Strings.resx` (Czech, the primary language), `Strings.sk.resx` and `Strings.en.resx`. The build generates the typed `Strings` class from them, used by view models and in XAML as `{x:Bind loc:Strings.Key}`. Add a key to all three files; a test fails if a translation is missing or its `{0}` placeholders differ.

## Solution layout

```text
src/
  WafeControl.Shared/    API client, models (source-generated JSON), session + auth handler
  WafeControl.Core/      Services and view models (CommunityToolkit.Mvvm), shared by every app
  WafeControl.WinUI/     Windows app (WinUI 3, Windows App SDK, unpackaged)
  WafeControl.Tests/     Tests for Shared + Core (xUnit.net v3 on Microsoft.Testing.Platform)
```

## Build and run

Requirements: .NET 10 SDK. The WinUI app builds on Windows only. The Windows App SDK is bundled (self-contained), so no runtime installer is needed.

```powershell
dotnet build WafeControl.slnx
dotnet test --solution WafeControl.slnx
dotnet run --project src/WafeControl.WinUI
```

`global.json` puts `dotnet test` in Microsoft.Testing.Platform mode, so pass the solution with `--solution` (or a project with `--project`). Visual Studio's Test Explorer runs the tests directly.

## Release

Push a version tag (`git tag v1.2.0 && git push origin v1.2.0`). `.github/workflows/release.yml` tests, publishes the WinUI app for x64 and ARM64, builds an installer for each with Inno Setup (`installer/WafeControl.iss`) and attaches them to a GitHub Release. Tags with a suffix (`v1.2.0-beta.1`) become pre-releases.

## Where data is stored

- **Remembered login:** `%AppData%\WafeControl\credentials.dat`, encrypted with Windows DPAPI for the current user. "Sign out" in the ⋯ menu deletes it.
- **Settings (WinUI app):** `%AppData%\WafeControl\settings.json` (language, theme, closing and startup behaviour, window position). Start with Windows is the `WafeControl` entry under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (also listed in Task Manager → Startup apps).
- **Logs (WinUI app):** `%LocalAppData%\WafeControl\logs`, kept for 7 days. Open them via ⋯ → "Open log folder". Passwords are never logged.
- **Versions up to 1.0.1** used `RecuperationSystem` folders and a `WafeRecuperation` startup entry; the app moves them on its first start (`LegacyInstallMigration`), and setup removes the old files and shortcuts.

## Wafe API

Base URL `https://go2my.wafe.eu/api/`.

| Call | Purpose |
| --- | --- |
| `POST auth/context` `{"username", "password"}` | Sign in; returns 201 with a `Sandcastle-Key` header |
| `GET api/v1/main` | Status: temperatures, CO₂, flow, mode, boost, filters, … (kebab-case JSON) |
| `PUT api/v1/main/stop-active` | Start/stop the unit |
| `PUT api/v1/main/flow-requested` | Flow rate, 50–220 m³/h (Manual mode) |
| `PUT api/v1/main/authority` | Operating mode |
| `PUT api/v1/main/boost-remaining` | Boost duration in seconds (0 stops it) |
| `PUT api/v1/main/silent-active`, `holiday-active` | Silent / Holiday mode |
| `GET api/v1/schedule` | Weekly plan: `{"modes": ["min","auto","nom","boost"], "plan": "boost-0:2:0-0:3:0 …"}` |
| `PUT api/v1/schedule/plan` | Replaces the **whole** plan (always send every entry) |

Plan entries are `mode-D:H:M-D:H:M` with day 0 = Monday, e.g. `boost-6:2:0-6:2:30` = Sunday 02:00–02:30.

All PUT bodies are `{"value": …}`. Requests carry the `Sandcastle-Key` header; on 401/403 the app signs in again and retries once.

Request bodies must be sent with a `Content-Length`: the server answers chunked bodies with 500.

## License

For personal use only. Not affiliated with Wafe.
