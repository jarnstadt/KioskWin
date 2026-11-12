# KioskWin — WebView2 Full-Screen Lockdown Application Design Specification

- **Date**: 2026-07-16
- **Status**: Approved (implementation planning pending)
- **Target platform**: Windows industrial PC (Win10/11 x64), single display
- **Implementation**: Option A — WinForms + self-contained single file + Evergreen WebView2 + startup-folder launch

---

## 1. Background and goals

Build a C# application that uses WebView2 to load and display a configurable remote web page
(default: `https://position.lmding.cn/public/wjbl/xcj/dist/index.html#/`).
The application runs on a Windows industrial PC and opens full-screen, **without minimize or close buttons and ignoring close or minimize actions**,
to prevent regular users from intentionally closing it or switching it to the background.

### Lockdown level: Basic

The threat model is confirmed to be a "regular operator" (someone who may click the close button, press Alt+F4, or try Alt+Tab).
Therefore, use **basic lockdown**: borderless full-screen, hidden buttons, and intercept Alt+F4 and window-close messages.
This does **not** include a watchdog process, low-level keyboard hooks, registry/group-policy lockdown, or replacing the Windows shell.

## 2. Requirements

### Functional requirements

| ID | Requirement |
|---|---|
| F1 | Load and display the URL specified in the configuration file full-screen |
| F2 | Configure the URL in the configuration file without recompiling |
| F3 | Start full-screen (borderless, covering the primary display), without minimize or close buttons |
| F4 | Intercept Alt+F4 and user-initiated close actions (`CloseReason.UserClosing`) |
| F5 | Prevent background switching: stay on top, stay off the taskbar, and reclaim focus when deactivated |
| F6 | Start automatically after boot without administrator privileges |
| F7 | Show a full-screen "Retrying" overlay after network or page-load failures, retry in the background, and return to the page when connectivity is restored |
| F8 | Hidden administrator escape path: hidden key combination plus password verification; provide Exit / Reload / DevTools / Unlock |
| F9 | Single instance: activate the existing window on repeated launches rather than opening another |

### Non-functional requirements

| ID | Requirement |
|---|---|
| N1 | Self-contained single-file deployment; no preinstalled .NET runtime required on the industrial PC |
| N2 | Unit-test coverage for testable pure logic (configuration, password hashing, retry state machine) |
| N3 | No silent crashes: log unhandled exceptions and display an error overlay |
| N4 | Use safe defaults and do not crash when configuration is missing or invalid |

## 3. Out of scope / known limitations (basic lockdown)

- **Does not intercept** Alt+Tab, the Windows key, Ctrl+Shift+Esc (Task Manager), or Ctrl+Alt+Del.
  A technically proficient user can end the process through Task Manager. `TopMost` plus focus reclamation keeps the window visible in the foreground,
  but cannot prevent forced termination.
- **No watchdog**: the process is not restarted after it is terminated.
- If stronger lockdown is needed later, this can be extended to "medium lockdown" (watchdog + keyboard hook) or
  "high-security kiosk" (custom shell + registry policies); see the evolution path in section 12.

## 4. Architecture and components

```
position-win/
├── KioskWin.sln
├── src/KioskWin/
│   ├── KioskWin.csproj          # WinForms; net8.0-windows; self-contained single-file publish
│   ├── Program.cs                    # Entry point: DPI, single-instance mutex, exception handling, logger initialization
│   ├── Forms/
│   │   ├── MainForm.cs(.Designer)    # Borderless full-screen window hosting WebView2
│   │   ├── RetryOverlay.cs(.Designer)# Full-screen "Network failure — retrying" overlay
│   │   └── AdminDialog.cs(.Designer) # Password field + action buttons
│   ├── Core/
│   │   ├── KioskConfig.cs            # Configuration POCO + load/validate/reload
│   │   ├── PasswordHasher.cs         # SHA-256(salt + pwd), testable pure functions
│   │   ├── RetryController.cs        # Retry state machine, testable pure logic
│   │   └── FileLogger.cs             # Writes to %LocalAppData%\KioskWin\logs\
│   ├── appsettings.json              # Configuration file (next to the exe)
│   └── install-shortcut.ps1          # One-click creation of a startup shortcut
├── tests/KioskWin.Tests/        # xUnit tests for pure Core-layer logic
└── docs/superpowers/specs/…
```

### Layering principles

- `Forms`: Win32 / WebView2 / window behavior (not unit-testable; cover with a manual QA checklist).
- `Core`: testable pure logic (configuration, password hashing, retry state machine), covered by xUnit.
- Keep unit responsibilities clear and independently understandable; communicate through explicit interfaces.

## 5. Window and basic lockdown behavior (`MainForm`)

| Behavior | Implementation |
|---|---|
| Borderless full-screen | `FormBorderStyle = FormBorderStyle.None` + `WindowState = FormWindowState.Maximized` (cover the primary display) |
| No minimize/close buttons | `None` style has no title bar or system buttons |
| Intercept Alt+F4 | In `FormClosing`, set `e.Cancel = true` when `e.CloseReason == CloseReason.UserClosing` and the close is **not administrator-authorized** |
| Allowed closing | Allow `WindowsShutDown` (system shutdown) and `ApplicationExitCall` (administrator exit) |
| Prevent background switching | `TopMost = true` (always on top) + `ShowInTaskbar = false` (no taskbar entry) + reclaim focus with `BeginInvoke(() => Activate())` in the `Deactivate` event |
| Keyboard preview | `KeyPreview = true` makes the form receive keystrokes before child controls (for the admin key combination) |

### Close-control state

`MainForm` maintains the Boolean field `private bool _authorizedClose;`, defaulting to `false`.
In `FormClosing`, cancel when `!_authorizedClose && e.CloseReason == UserClosing`.
The administrator's "Exit" button sets `_authorizedClose = true` before calling `Application.Exit()`.

## 6. WebView2 configuration and navigation (`MainForm`)

### Initialization

`EnsureCoreWebView2Async(null)` → **Evergreen Runtime** (default; the machine is online and the runtime is bootstrapped on first launch).
After `InitializationCompleted` succeeds, configure `CoreWebView2Settings`:

- `AreDevToolsEnabled = false`
- `AreBrowserAcceleratorKeysEnabled = false` (disable F12 / Ctrl+F / Ctrl+P / Ctrl+plus zoom, etc.)
- `IsStatusBarEnabled = false`
- `IsZoomControlEnabled = false`
- `IsBuiltInErrorPageEnabled = false` (we render our own error page)
- `IsGeneralAutofillEnabled = false`

### Navigation and retry flow

- Listen for `NavigationCompleted`: if `!e.IsSuccess`, show `RetryOverlay` and start `RetryController`.
- `RetryController` uses an internal `System.Windows.Forms.Timer` to call
  `CoreWebView2.Navigate(Url)` every `RetryIntervalSeconds`; on successful navigation, hide the overlay and stop the timer.
- Listen for `CoreWebView2.ProcessFailed` (renderer crash) and reinitialize/reload WebView2.

> Interface contract: `RetryController` exposes `event Action ShouldRetry` and pure state transitions
> (Idle / Waiting / Retrying), without `Show()/Hide()`, so the state machine can be tested without UI dependencies.
>
> **`RetryOverlay` is the only full-screen overlay component** and can be reused for different scenarios by changing only its text/state:
> network retry ("Network connection failed. Retrying…"), configuration error ("The URL is invalid. Contact your administrator."),
> missing WebView2 runtime, and generic error pages for unhandled exceptions. Do not create a separate component for each scenario.

## 7. Administrator escape path (`AdminDialog`)

### Trigger

`KeyPreview=true` + override `ProcessCmdKey(ref Message, Keys)` to detect the key combination
(default `Ctrl+Shift+Alt+Q`, configurable through `AdminKeyCombination`); consume the keystroke and show `AdminDialog` when matched.
Regular users are unlikely to discover the combination.

### Authentication

- Password field `TextBox`, `PasswordChar = '*'`.
- On submit, call `PasswordHasher.Verify(input, config.AdminPasswordHash, config.PasswordSalt)`,
  which uses `SHA-256(salt + input)` and a constant-time comparison.
- Enable the four action buttons below only after successful verification; on failure, show a message, clear the field, and log the cumulative failure count.

### Action buttons

| Button | Behavior |
|---|---|
| Exit | Set `_authorizedClose = true`, then call `Application.Exit()` |
| Reload page | Reload the configuration and call `CoreWebView2.Navigate(config.Url)` |
| Open DevTools | Temporarily set `AreDevToolsEnabled = true` and call `OpenDevToolsWindow()` |
| Unlock | Temporarily restore a normal window: `TopMost=false` + `FormBorderStyle=Sizable` + `WindowState=Normal`, so the window can be moved during maintenance |

> **About "minimize"**: Because `ShowInTaskbar=false` and `TopMost=true`, traditional "minimize to taskbar" behavior would make the application
> inaccessible from the taskbar. Therefore, "Unlock (restore normal window)" is used instead and is better suited to maintenance.

## 8. Configuration file `appsettings.json`

```json
{
  "Url": "https://position.lmding.cn/public/wjbl/xcj/dist/index.html#/",
  "AdminKeyCombination": "Ctrl+Shift+Alt+Q",
  "AdminPasswordHash": "<SHA-256 hex; see generation instructions below>",
  "PasswordSalt": "<random hex>",
  "RetryIntervalSeconds": 10,
  "TopMost": true,
  "ShowInTaskbar": false,
  "UserDataFolder": "WebView2Data"
}
```

- Load with `Microsoft.Extensions.Configuration.Json`. Read once at startup; the "Reload page" action reloads it.
- Validation: `Url` must be a valid absolute HTTP/HTTPS URL; `RetryIntervalSeconds` must be in [1, 600].
- If configuration is missing/invalid, use the defaults above for other fields and log a warning. Do not fall back to `about:blank` for an invalid `Url`;
  instead, show the `RetryOverlay` "Configuration error" state ("The URL is invalid. Contact your administrator.") without crashing.
  After correcting the configuration, use the administrator "Reload page" action to reload it.

### Generate a password hash

The application includes a hidden CLI mode:

```
KioskWin.exe --hash-password <password>
```

Print the generated `AdminPasswordHash` and random `PasswordSalt`, then add them to `appsettings.json`.
**Never store a plaintext password in the configuration.**

## 9. Packaging, deployment, and startup

### Publish command

```
dotnet publish src/KioskWin/KioskWin.csproj \
  -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true
```

The output is a single exe; **no .NET installation is required on the industrial PC**.

> **Known consideration**: WebView2's native loader (`WebView2Loader.dll`) requires
> `IncludeNativeLibrariesForSelfExtract=true` in single-file mode and is extracted to a temporary directory at runtime. Verify that WebView2 initializes correctly;
> if single-file publishing conflicts with WebView2, disable `PublishSingleFile` as a fallback (still self-contained, but deployed as a multi-file directory).

### Deployment

- Copy the publish output to `C:\KioskWin\`.
- Keep `appsettings.json` next to the exe; the URL can be changed without recompiling.

### Startup on boot (no administrator privileges required)

- `install-shortcut.ps1` creates a shortcut in the current user's Startup folder:
  `%AppData%\Microsoft\Windows\Start Menu\Programs\Startup\KioskWin.lnk`
- The Startup-folder approach is visible, easy to manage, and does not require elevation.
- **Optional enhancement**: include the Evergreen bootstrapper `MicrosoftEdgeWebview2Setup.exe` in the package;
  if the first call to `EnsureCoreWebView2Async` throws a "runtime not installed" exception, silently run the bootstrapper and retry.

## 10. Error handling and logging

| Scenario | Handling |
|---|---|
| WebView2 runtime not detected | Catch the `EnsureCoreWebView2Async` exception and show a full-screen overlay; optionally run the bootstrapper automatically |
| Renderer crash | `CoreWebView2ProcessFailed` → reinitialize/reload |
| Unhandled exception | `AppDomain.UnhandledException` + `Application.ThreadException` → write to the log and show a generic error overlay; do not exit silently |
| Second instance | Named `Mutex` (`Global\KioskWin_SingleInstance`) → bring the existing window to the foreground and exit the new process |
| Missing/invalid configuration | Safe defaults + log warning |

### Logs

- Path: `%LocalAppData%\KioskWin\logs\yyyy-MM-dd.log`
- Contents: startup, URL, navigation results (success/failure), retry events, exception stacks, and administrator actions (authentication success/failure, selected action).
- Implementation: lightweight `FileLogger` (append-only, one file per day), without heavy dependencies.

## 11. Testing strategy

### Unit tests (xUnit, `tests/KioskWin.Tests/`)

- `KioskConfig`: valid JSON parsing, field validation, defaults for missing/invalid fields, and hot reload.
- `PasswordHasher`: accept the correct password, reject incorrect passwords, produce different hashes for different salts, and use constant-time comparison.
- `RetryController`: state transitions Idle→Waiting→Retrying→(Idle on success / remain Retrying on failure),
  timer start/stop, and event firing.
- URL validation.

### Manual QA checklist (window/Win32 behavior)

- [ ] Full-screen over the primary display, with no title bar or minimize/close buttons
- [ ] Alt+F4 has no effect; the window does not close
- [ ] No taskbar entry
- [ ] Window stays on top and remains visible in the foreground after Alt+Tab
- [ ] Network disconnect → retry overlay appears → network restored → page returns automatically
- [ ] Admin key combination opens the password dialog
- [ ] Incorrect password is rejected; correct password enables the action buttons
- [ ] Exit / Reload / DevTools / Unlock all behave as expected
- [ ] Startup on boot works
- [ ] Repeated launch activates only the existing window

### Smoke deployment

Run the published output on a physical industrial PC or a Win10/11 virtual machine and complete the QA checklist.

## 12. Evolution path (out of scope for this release)

For stronger lockdown in the future:

- **Medium lockdown**: add a separate watchdog process (restart the main process if it is terminated) + low-level keyboard hook (`SetWindowsHookEx`) to block Alt+Tab/Win/Ctrl+Shift+Esc.
- **High-security kiosk**: register a custom shell (replace `explorer.exe`) to launch on boot + disable Task Manager through registry/group policy + package a fixed-version WebView2 offline runtime.

## 13. Decision record

| Decision | Choice | Rationale |
|---|---|---|
| Lockdown level | Basic | Threat model is a regular operator; YAGNI |
| UI framework | WinForms | Minimal code for borderless full-screen; fast to build |
| WebView2 runtime | Evergreen | Machine is online; automatic maintenance; no offline runtime needed |
| Packaging | Self-contained single file | No .NET installation needed on the industrial PC; copy and run |
| Startup | Startup-folder shortcut | No elevation; visible and easy to manage |
| Maintenance exit | Hidden key combination + password | Regular users are unlikely to discover it; convenient for maintenance |
| "Minimize" semantics | Unlock (normal window) | Minimizing to the taskbar is not useful with `ShowInTaskbar=false` |
