# KioskWin

KioskWin is a Windows kiosk shell for opening a configured web page in a full-screen WebView2 window. It is intended for locked-down display stations where the app should start automatically, stay in front, retry failed page loads, and expose a password-protected maintenance dialog.

## Features

- Full-screen borderless Windows Forms host with WebView2.
- Configurable target URL, retry interval, top-most mode, taskbar visibility, and WebView2 data folder.
- Optional automatic page fitting with hidden scrollbars via `AutoFitToWindow`.
- Admin dialog with password-protected exit, DevTools, and unlock actions, plus password-free reload and cache clearing.
- Global admin escape hotkey, defaulting to `Ctrl+Shift+Alt+Q`.
- Direct unlock with the configured admin escape hotkey when no admin password is configured.
- Touch access to maintenance by holding the invisible top-right corner for three seconds.
- Startup shortcut install and uninstall PowerShell scripts included in publish output.

## Requirements

- Windows.
- .NET SDK with `net8.0-windows` support.
- Microsoft Edge WebView2 Runtime on target machines.

## Configuration

Runtime settings are read from `appsettings.json` beside `KioskWin.exe`.

```json
{
  "Url": "https://www.baidu.com",
  "AdminKeyCombination": "Ctrl+Shift+Alt+Q",
  "AdminPassword": "",
  "RetryIntervalSeconds": 10,
  "TopMost": true,
  "ShowInTaskbar": false,
  "UserDataFolder": "WebView2Data",
  "AutoFitToWindow": true
}
```

Set `AdminPassword` in the deployed `appsettings.json`. It is stored as plain text: restrict access to the file and do not commit a real password. The former `AdminPasswordHash` and `PasswordSalt` settings are no longer supported; remove them and set `AdminPassword` when upgrading. The application rejects non-empty legacy settings rather than silently leaving the kiosk unlocked.

If `AdminPassword` is empty or whitespace, pressing the configured `AdminKeyCombination` switches the kiosk into temporary unlocked mode and opens the maintenance dialog. The default escape hotkey is `Ctrl+Shift+Alt+Q`.

On a touch screen, hold the top-right 48-by-48-pixel corner for three seconds. The touch target is invisible but intercepts touches in that area. With a configured password, the admin dialog requests the built-in Windows touch keyboard; verify the password and choose **Exit**. Windows may decline the request or choose not to show the keyboard depending on its settings and attached hardware. Without a configured password, the gesture unlocks the window, allows it to be closed, and opens the maintenance dialog. This means anyone with physical access can exit an unconfigured kiosk. Configure `AdminPassword` to require authentication.

Verify touch behavior on the target Windows device: the corner must remain transparent over changing page content and error overlays; releasing early or moving outside it must cancel the gesture. Check that both reload actions work before authentication, cached content is refreshed, and cookies and local storage remain intact. When a password is configured, check that the Windows touch keyboard is accessible and an incorrect password cannot enable protected actions.

The admin dialog's **Reload page** and **Clear cache and reload** actions are available without password verification. The latter clears WebView2's disk cache and CacheStorage for the entire browser profile, then reloads the configured URL. Cookies and other site data such as local storage are not cleared. Exit, DevTools, and unlock actions in the dialog require a configured admin password.

## Build and Test

Restore, build, and test from the repository root:

```powershell
dotnet restore KioskWin.sln
dotnet build KioskWin.sln -c Release
dotnet test KioskWin.sln -c Release --no-restore --nologo
```

Create a self-contained `win-x64` publish directory and zip archive:

```cmd
build.bat
```

Use `build.bat notest` to skip tests during packaging.

GitHub Actions builds and tests the project on pushes and pull requests to `main` or `master`. Pushing a tag also creates a GitHub release containing `KioskWin-win-x64.zip` after the build and tests succeed:

```bash
git tag v1.0.0
git push origin v1.0.0
```

In the **Build** workflow's **Run workflow** form, leave `release_tag` empty for a build-only run, or enter an existing remote tag to build that tag and create its release. The release step fails if the tag does not exist or a release already exists. The workflow must first be pushed to the repository's default branch before it appears in the Actions tab. Repository Actions settings must allow the workflow to write contents for release creation.

## Deployment

After packaging, copy `publish\` or `KioskWin-win-x64.zip` to the target machine, edit `appsettings.json`, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-shortcut.ps1
```

To remove the startup shortcut:

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall-shortcut.ps1
```

## Project Layout

- `src/KioskWin/`: application code.
- `src/KioskWin/Core/`: config, parsing, logging, retry, and helper logic.
- `src/KioskWin/Forms/`: Windows Forms UI.
- `tests/KioskWin.Tests/`: xUnit tests.
- `build.bat`: restore, test, publish, and zip script.
