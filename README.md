# KioskWin

KioskWin is a Windows kiosk shell for opening a configured web page in a full-screen WebView2 window. It is intended for locked-down display stations where the app should start automatically, stay in front, retry failed page loads, and expose a password-protected maintenance dialog.

## Features

- Full-screen borderless Windows Forms host with WebView2.
- Configurable target URL, retry interval, top-most mode, taskbar visibility, and WebView2 data folder.
- Optional automatic page fitting with hidden scrollbars via `AutoFitToWindow`.
- Password-protected admin dialog for exit, reload, DevTools, and temporary unlock mode.
- Global admin escape hotkey, defaulting to `Ctrl+Shift+Alt+Q`.
- Direct unlock with the configured admin escape hotkey when no admin password hash and salt are configured.
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
  "AdminPasswordHash": "",
  "PasswordSalt": "",
  "RetryIntervalSeconds": 10,
  "TopMost": true,
  "ShowInTaskbar": false,
  "UserDataFolder": "WebView2Data",
  "AutoFitToWindow": true
}
```

Generate admin password values with:

```powershell
.\KioskWin.exe --hash-password <password>
```

Copy the generated `AdminPasswordHash` and `PasswordSalt` values into `appsettings.json`. If either value is empty, pressing the configured `AdminKeyCombination` directly switches the kiosk into temporary unlocked mode. The default escape hotkey is `Ctrl+Shift+Alt+Q`.

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

GitHub Actions also builds and tests the project on pushes and pull requests to `main` or `master`. Run the **Build** workflow manually with `workflow_dispatch` to produce downloadable `KioskWin-win-x64` artifacts.

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
- `src/KioskWin/Core/`: config, parsing, logging, retry, hashing, and helper logic.
- `src/KioskWin/Forms/`: Windows Forms UI.
- `tests/KioskWin.Tests/`: xUnit tests.
- `build.bat`: restore, test, publish, and zip script.
