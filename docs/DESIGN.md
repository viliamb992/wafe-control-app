# WAFE Control — Design Manual

The rules both apps follow. The Windows app gets most of them for free from WinUI 3 (Fluent 2); the mobile
app (.NET MAUI) re-creates the same look with the tokens below. **When the two disagree, this file wins**:
fix the app, or change this file first and then both apps.

Implementation: `src/WafeControl.Mobile/Resources/Styles/Colors.xaml` (tokens) and `Styles.xaml`
(components). Views use only named tokens and styles, never raw colors or sizes.

---

## 1. Principles

1. **Fluent, calm, native.** Same structure and visual language as Windows 11 Settings: neutral surfaces,
   cards with a hairline stroke, one accent color, outline icons. No gradients, no heavy shadows, no decoration.
2. **State first.** The first screen answers "is the unit running, in which mode, and is the air good?"
   before any control.
3. **One accent.** Brand blue marks what is interactive or selected. Status uses the semantic colors
   (success, caution, critical), never the accent.
4. **Every command shows its progress.** A control that sends something to the unit shows a pending state
   until the unit confirms (spinner or disabled), and the result appears as a status message.
5. **Same words everywhere.** All text comes from `WafeControl.Core` `Strings` (cs, sk, en). No text in views.

---

## 2. Color

### 2.1 Brand

| Token | Value | Use |
| --- | --- | --- |
| `Brand` | `#3090DC` | App icon, splash screen, Android launcher background. Not for text or controls (3.4:1 on white). |

### 2.2 Theme tokens

Values are opaque equivalents of the WinUI theme resources (in brackets), so the apps match side by side.

| Token | Light | Dark | WinUI equivalent / use |
| --- | --- | --- | --- |
| `PageBackground` | `#F3F3F3` | `#202020` | Mica / `SolidBackgroundFillColorBase`. Page and navigation bars. |
| `CardBackground` | `#FFFFFF` | `#2B2B2B` | `CardBackgroundFillColorDefault`. Cards, tiles, sheets. |
| `CardStroke` | `#E5E5E5` | `#1C1C1C` | `CardStrokeColorDefault`. 1 px around every card. |
| `Divider` | `#EBEBEB` | `#3D3D3D` | `DividerStrokeColorDefault`. Between rows inside a card. |
| `ControlFill` | `#FBFBFB` | `#373737` | `ControlFillColorDefault`. Standard buttons, selected segment. |
| `ControlStroke` | `#E0E0E0` | `#434343` | `ControlStrokeColorDefault`. |
| `ControlAltFill` | `#F3F3F3` | `#242424` | `ControlAltFillColorSecondary`. Segmented track, input backgrounds. |
| `ControlStrong` | `#8A8A8A` | `#9D9D9D` | `ControlStrongFillColorDefault`. Slider rail, "stopped" state, off switch track. |
| `TextPrimary` | `#1B1B1B` | `#FFFFFF` | `TextFillColorPrimary`. |
| `TextSecondary` | `#5F5F5F` | `#C8C8C8` | `TextFillColorSecondary`. Labels, descriptions, captions. |
| `TextTertiary` | `#8A8A8A` | `#9E9E9E` | `TextFillColorTertiary`. Placeholders, the time ruler. |
| `TextDisabled` | `#A3A3A3` | `#6E6E6E` | `TextFillColorDisabled`. |
| `Accent` | `#1A6DB5` | `#78BAEE` | `AccentFillColorDefault`. Accent buttons, switch on, slider, progress, selection. |
| `AccentText` | `#155A96` | `#99CCF3` | `AccentTextFillColorPrimary`. Links, accent icons. |
| `TextOnAccent` | `#FFFFFF` | `#000000` | `TextOnAccentFillColorPrimary`. |
| `Success` | `#0F7B0F` | `#6CCB5F` | `SystemFillColorSuccess`. Running, online, good air. |
| `Caution` | `#9D5D00` | `#FCE100` | `SystemFillColorCaution`. Fair air, filter wearing out. |
| `Critical` | `#C42B1C` | `#FF99A4` | `SystemFillColorCritical`. Offline, poor air, errors. |
| `Attention` | `#005FB7` | `#60CDFF` | `SystemFillColorAttention`. Informational banners, humidity. |
| `SuccessBackground` | `#DFF6DD` | `#393D1B` | InfoBar backgrounds. |
| `CautionBackground` | `#FFF4CE` | `#433519` | |
| `CriticalBackground` | `#FDE7E9` | `#442726` | |
| `AttentionBackground` | `#F6F6F6` | `#272727` | |

Contrast (WCAG): `Accent` with `TextOnAccent` ≥ 5.3:1, `TextSecondary` on cards ≥ 5.5:1, all semantic
colors ≥ 5:1 on their theme's card background.

Windows follows the user's Windows accent color; mobile has no system accent and uses `Accent` above.

### 2.3 Fixed colors (same in both themes)

Schedule modes. White text, identical in both apps (`WinUI/Helpers/Xaml.cs` → `ScheduleModeBrushes`).

| Mode | Color |
| --- | --- |
| `min` Minimum | `#2F6FD6` |
| `auto` Intelligent | `#2E8B57` |
| `nominal` Nominal | `#B86E0A` |
| `boost` Boost (matches the Wafe web app) | `#BC3F3A` |
| unknown | `#6B7280` |

### 2.4 Meaning of color

| Thing | Color |
| --- | --- |
| Unit running / stopped | `Success` / `ControlStrong` circle behind the power icon |
| Online / offline dot | `Success` / `Critical` |
| CO₂ < 800 / < 1200 / ≥ 1200 ppm / no reading | `Success` / `Caution` / `Critical` / `TextSecondary` |
| Sensor icons | Outdoor `Caution`, supply air `Success`, indoor `AccentText`, exhaust air `TextSecondary`, humidity `Attention` |
| Filter health ≥ 20 % / < 20 % | `Accent` / `Caution` bar |

Never rely on color alone: every colored state also has a text (Running, Online, "Good air quality", 34 %).

---

## 3. Typography

System font (Segoe UI Variable on Windows, Roboto on Android, SF Pro on iOS). No bundled text fonts.
Mobile sizes follow the Fluent type ramp, body +1 for reading distance on phones.

| Style | Windows | Mobile | Weight | Use |
| --- | --- | --- | --- | --- |
| `Caption` | 12 | 12 | Regular | Secondary lines, legends, footers |
| `Body` | 14 | 15 | Regular | Default text |
| `BodyStrong` | 14 | 15 | Semibold | Card titles, row titles, button text |
| `BodyLarge` | 18 | 18 | Regular | Sheet headers |
| `Subtitle` | 20 | 20 | Semibold | Page section titles, the unit's state |
| `Metric` | 20 | 24 | Semibold | Sensor values |
| `Title` | 28 | 28 | Semibold | Login heading, dashboard title (unit name) |

Semibold = Roboto Medium (`sans-serif-medium`) on Android, bold on iOS. Sentence case everywhere, no
all-caps buttons. Numbers keep their unit with a space: `21,5 °C`, `140 m³/h`, `650 ppm`, `34 %`.

---

## 4. Layout and spacing

4 px grid. Allowed values: **4, 8, 12, 16, 20, 24, 32**.

| Rule | Windows | Mobile |
| --- | --- | --- |
| Page gutter | 24 | 16 |
| Content max width | 1100 | 1100 |
| Gap between cards | 16 | 12 |
| Card padding | 20 (settings rows 16) | 16 |
| Title → content inside a card | 12 | 12 |
| Label → value | 4 | 4 |
| Sensor tiles | min 160 wide, 12 apart | 2 per row on phones, 3 at ≥ 600 dp, 12 apart |
| Columns | 2 at ≥ 820 px | 1 on phones, 2 at ≥ 700 dp (tablets, landscape) |

Touch targets on mobile are at least **44 × 44 dp**, even where the visual is smaller (chips, icon buttons).

## 5. Shape and elevation

| Element | Windows | Mobile |
| --- | --- | --- |
| Card, tile, banner | 8 | 12 |
| Button, input, segmented track | 4 | 8 |
| Segment, schedule block | 4 | 6 |
| Chip, status pill, badge | 4 | fully rounded (pill) |
| Sheet (modal) | 8 | platform (iOS page sheet; full screen on Android) |

Radii on mobile are one step larger than WinUI (touch, modern phone UI) and nest: inner radius = outer − padding
where the gap is small.

Elevation: cards and tiles are **flat** (1 px `CardStroke`, no shadow). Only floating things get a shadow:
the status toast (`#000000`, 14 % opacity, offset 0/8, blur 16); sheets use the platform's own.

---

## 6. Iconography

**Fluent System Icons, Regular** (MIT, same style as Segoe Fluent Icons on Windows). The mobile app ships a
subset font `FluentIcons.ttf`; glyphs are named in `Helpers/FluentIcons.cs`. Add a glyph by adding it to
`tools/subset-icons.py` and re-running it.

| Size | Use |
| --- | --- |
| 16 | Inline with caption/body text (tile labels, chips, pills) |
| 20 | Card titles, setting rows, banners |
| 24 | Tab bar, icon buttons, the power button |

Outline only; color is `TextPrimary` unless a semantic color applies (section 2.4). Same concept → same icon in
both apps:

| Concept | Windows (Segoe Fluent) | Mobile (Fluent System) |
| --- | --- | --- |
| Power | `E7E8` | `power` |
| Outdoor | `EC8A` | `weather_sunny` |
| Supply air | `E8BE` | `arrow_import` |
| Indoor | `E80F` | `home` |
| Exhaust air | `E898` | `arrow_export_ltr` |
| CO₂ | `EA91` | `molecule` |
| Humidity | `EB42` | `drop` |
| Boost | `E945` | `flash` |
| Silent | `E708` | `weather_moon` |
| Holiday | `E709` | `airplane` |
| Schedule | `E787` | `calendar_ltr` |
| Next start | `E823` | `clock` |
| Settings | `E713` | `settings` |
| Language | `F2B7` | `local_language` |
| Appearance | `E790` | `dark_theme` |
| Unit | `E772` | `cube` |
| Rename | `E8AC` | `rename` |
| Service | `E779` | `person_support` |
| Sign-in method | `E928` | `fingerprint` |
| Explain (?) | `E9CE` | `question_circle` |
| New version | `E896` | `arrow_download` |

---

## 7. Components

### Card
`CardBackground`, 1 px `CardStroke`, radius 12, padding 16. Title in `BodyStrong`, optional 20 px icon left of it.
A card groups one topic (Ventilation, Boost, Modes, Filters). Don't nest cards.

### Sensor tile
A small card (padding 16/12): 16 px colored icon + `Caption` label on one line, `Metric` value below, optional
`Caption` status (CO₂ quality in its semantic color). Unknown values show `–`.

### State header (dashboard top card)
56 px circle (`Success` running / `ControlStrong` stopped) with a 24 px power icon in `TextOnAccent`, state
in `Subtitle`, summary in `Caption`/`TextSecondary`, the power button on the right (below on narrow phones).
Stopping asks for confirmation; starting does not.

### Buttons
| Kind | Look | Use |
| --- | --- | --- |
| Accent | `Accent` fill, `TextOnAccent`, no stroke | The one primary action of a screen (Sign in, Save) |
| Standard | `ControlFill`, 1 px `ControlStroke`, `TextPrimary` | Everything else (Start/Stop, Rename) |
| Subtle | Transparent, `TextPrimary` | Toolbar and icon buttons |
| Danger text | Transparent, `Critical` text | Delete in sheets |

Height 44, radius 8, text `BodyStrong`, horizontal padding 16. Pending: text replaced by a 20 px spinner,
button disabled. Disabled: 40 % opacity.

### Chip (boost durations)
Pill, height 36 (44 touch), `ControlFill` + `ControlStroke`, `BodyStrong`. The running boost's stop chip uses
`Accent` fill.

### Segmented control (operating mode, schedule day)
`ControlAltFill` track, radius 8, 2 px inset. The selected segment is `ControlFill` with a 1 px `ControlStroke`
and radius 6, text `BodyStrong`, plus a 16 × 3 `Accent` pill under the text, like the WinUI toolkit Segmented.
Others: `TextSecondary`, `Body`. Height 40. Tapping sends immediately; the control is disabled while pending.

### Switch
Native switch. On: `Accent` track, white thumb. Off: `ControlStrong`. In a setting row it sits at the right,
vertically centered.

### Slider (flow rate)
`Accent` for the filled part and thumb, `ControlStrong` for the rail. Value shown above right in `BodyStrong`.
Sends when the finger lifts; a haptic tick every 10 m³/h while dragging.

### Progress bar (filters)
4 px, rounded, `ControlAltFill` rail. Label left (`Body`), percentage right (`Caption`/`TextSecondary`).

### Setting row
Like Windows Settings: 20 px icon, title (`Body`) + description (`Caption`/`TextSecondary`) that wraps, the
control on the right. Rows in one card are separated by a 1 px `Divider` with 12 px space around it.

### Banner (InfoBar)
Radius 12, 1 px `CardStroke`, severity background (section 2.2), 20 px severity icon in the severity color,
title `BodyStrong`, message `Body`, optional action as a Standard button. Used for: no internet, unit offline,
schedule mode off, errors. Not dismissable when it describes a current state.

### Status pill
Pill with `ControlAltFill`, 8 px dot + `Caption` text. Online state in the dashboard header.

### Status toast
Shows `AppViewModel.StatusMessage` changes caused by the user's commands (e.g. "Boost activated for 15 minutes").
Bottom, above the tab bar, 16 from the edges, `CardBackground`, 1 px `CardStroke`, shadow, radius 12, `Body`.
Fades in 150 ms, stays 3 s, fades out 250 ms. Replaces the Windows footer status line.

### Sheet (add/edit schedule action, rename unit)
Modal page (iOS page sheet). Header row: Cancel (Subtle) · title (`BodyLarge` semibold) · Save (Accent text).
Content in a card, errors in a Critical banner inside the sheet. The sheet stays open until the unit accepted
the change. A choice between a few named options (the schedule mode) is a list in a card: 16 px color swatch or
icon, name, and an `Accent` checkmark on the selected row; rows 44 dp, separated by dividers.

### Schedule day
Day segmented control (Mon…Sun) above a card with a 24 h timeline: 30-minute rows 24 dp high, hour labels in
`Caption`/`TextTertiary` on the left, actions as blocks in their mode color (section 2.3) with white
`Caption` semibold text, radius 6. Tap an empty row to add, tap a block to edit. A "now" line in `Critical`.
Legend with 12 px rounded squares below. Swipe the timeline left/right for the next/previous day (it slides a third
of its width and fades, 150 ms out, 250 ms in). Hold a day in the segmented control (500 ms, long-press haptic), or
use the copy icon button next to the day's name, to open the copy sheet: the other days as a checkmark list; the
chosen days' actions are replaced.

### Empty, loading, error
- Loading a screen: centered 32 px spinner with a `Body`/`TextSecondary` line.
- Pull to refresh on the dashboard (accent spinner).
- Errors: a Critical banner at the top of the content, with the Core message.

---

## 8. Motion

Fluent timing: **150 ms** for small changes (fades, pressed states), **250 ms** for things entering or
leaving (toast, sheet), easing *cubic out*. No looping animations except spinners. Respect the platform's
reduce-motion setting.

## 9. Theming

Light, dark or system (Settings → Appearance), identical options on both platforms. Every color is a token
with a light and a dark value (`AppThemeBinding` on mobile, `ThemeResource` on Windows); a view that sets a
raw color is a bug.

## 10. Checklist for a new screen

- [ ] Only tokens and styles from this manual; no raw colors, sizes or fonts in the view.
- [ ] All text from `Strings`, present in cs, sk and en.
- [ ] Works in light and dark, and with cs (longest words) on a 360 dp wide phone.
- [ ] Every command has a pending state and a result message.
- [ ] Touch targets ≥ 44 dp; icons have an accessible name (`SemanticProperties.Description` / `AutomationProperties.Name`).
- [ ] Compiled bindings (`x:DataType`) on mobile, `x:Bind` on Windows.
