# Changelog

What changed in each version. The release workflows copy a version's section into the GitHub release, and the Windows app shows it when it offers the update. Windows and Android are released separately (`v1.2.0`, `android-v1.2.0`) but share version numbers.

## 1.3.0

- **Sign-in methods (Android):** Settings → Sign-in. "Stay signed in" keeps you signed in for 1, 14, 30 (default) or 90 days after you last typed your password, then asks for it again. "Fingerprint or face" asks for your fingerprint or face each time the app opens and when you return after 5 minutes, with no time limit. After signing in, the app asks once whether to use fingerprint or face. A login remembered by an earlier version counts its days from the first start of this one. The (?) next to each option explains it.
- **One copy at a time (Windows):** starting the app again brings the running window back, even from the tray. This now also works when the other copy is in a different folder, for example an older install.
- **Commands:** no more "Waiting for the unit to confirm…" step or "hasn't confirmed yet" warning. The "sending" text stays until the result; a late confirmation shows the usual success message.

## 1.2.0

- **Updates on Windows:** a new version downloads in the background; the title bar then offers "Restart to update", which takes about a second. A ready update also installs when you exit. Settings → Updates: download automatically, beta versions, check now. The app now installs per user without administrator rights; uninstall 1.1.x first (settings and the remembered login stay; turn "Run when Windows starts" on again if you used it).
- **Demo mode:** "Try the demo" on the sign-in screen opens a simulated unit. A banner says so on every screen, with a way to sign in with your own account.
- **Clearer errors:** messages say what went wrong (no connection, server problem, sign-in expired, refused) instead of showing technical text, with Retry where it can help.
- **Commands:** a change the unit confirms late is still announced, and a failed one returns to the unit's state. Windows asks before stopping the unit, like Android.
- **Old data:** a banner says when the readings are out of date, the server can't be reached or there is no internet, and the readings are dimmed. Polling slows down while the server is unreachable.
- **Sign-in help:** which account to use, what to do without a password or account, Caps Lock warning (Windows), show password (Android), and a note that the app is unofficial.
- **Report a problem** (Settings and the ⋯ menu): opens a GitHub issue with the app version and device filled in, or shares the logs and diagnostics (Android) / copies them (Windows). After a crash, the next start offers it.
- **Crash reports (opt-in):** after the first sign-in the app asks whether to send anonymous crash reports; change it in Settings. Emails are never sent.
- **Android:** the app now keeps a log file (7 days); share it from Settings → About.
