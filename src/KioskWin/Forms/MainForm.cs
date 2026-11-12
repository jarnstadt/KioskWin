using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using KioskWin.Core;
using System.Drawing;
using System.Windows.Forms;
using KioskWin.Forms;
using System.Runtime.InteropServices;

namespace KioskWin.Forms;

public sealed class MainForm : Form
{
    private const int AdminHotKeyId = 0x5101;
    private const int WM_HOTKEY = 0x0312;

    private KioskConfig _config;
    private readonly FileLogger _logger;
    private Keys _adminCombo;

    private readonly WebView2 _webView;
    private readonly RetryOverlay _overlay;
    private readonly AdminTouchTarget _adminTouchTarget;
    private readonly RetryController _retry;
    private readonly FormsRetryScheduler _scheduler;

    private bool _authorizedClose;
    private bool _devToolsEnabled;
    private bool _showingAdminDialog;
    private bool _adminHotKeyRegistered;

    public MainForm(KioskConfig config, FileLogger logger)
    {
        _config = config;
        _logger = logger;
        _adminCombo = KeyCombinationParser.Parse(config.AdminKeyCombination);

        // F3: borderless fullscreen with no minimize or close buttons
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;

        // F5: prevent switching to the background
        TopMost = config.TopMost;
        ShowInTaskbar = config.ShowInTaskbar;
        KeyPreview = true;
        BackColor = Color.Black;

        _webView = new WebView2 { Dock = DockStyle.Fill };
        _overlay = new RetryOverlay { Visible = false };
        _adminTouchTarget = new AdminTouchTarget(message => _logger.Log(message))
        {
            Location = new Point(ClientSize.Width - 48, 0),
        };
        _adminTouchTarget.HoldCompleted += OnAdminTouchHold;

        Controls.Add(_webView);
        Controls.Add(_overlay);
        Controls.Add(_adminTouchTarget);
        _overlay.BringToFront();
        _adminTouchTarget.BringToFront();

        _scheduler = new FormsRetryScheduler();
        _retry = new RetryController(config.RetryInterval, _scheduler);
        _retry.ShouldRetry += OnRetry;

        Load += OnLoad;
        FormClosing += OnFormClosing;   // F4
        Deactivate += OnDeactivate;     // F5
        Resize += OnResize;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterAdminHotKey();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterAdminHotKey();
        base.OnHandleDestroyed(e);
    }

    // ---- WebView2 initialization and navigation ----

    private async void OnLoad(object? sender, EventArgs e)
    {
        try
        {
            var userDataFolder = Path.Combine(AppContext.BaseDirectory, _config.UserDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);

            ConfigureWebView();

            _webView.CoreWebView2.ProcessFailed += OnProcessFailed;
            _webView.NavigationCompleted += OnNavigationCompleted;

            _logger.Log($"[startup] navigating to {_config.Url}");
            NavigateOrShowConfigError();
        }
        catch (Exception ex)
        {
            // For example, when the WebView2 runtime is not detected.
            _logger.Log($"[webview2 init] {ex}");
            _overlay.ShowRuntimeMissing();
            _overlay.Visible = true;
            _overlay.BringToFront();
            _adminTouchTarget.BringToFront();
        }
    }

    private void ConfigureWebView()
    {
        var s = _webView.CoreWebView2!.Settings;
        s.AreDevToolsEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
        s.IsStatusBarEnabled = false;
        s.IsZoomControlEnabled = false;
        s.IsBuiltInErrorPageEnabled = false;
        s.IsGeneralAutofillEnabled = false;
    }

    private void NavigateOrShowConfigError()
    {
        if (_webView.CoreWebView2 == null) return;

        if (_config.IsUrlValid)
        {
            _webView.CoreWebView2.Navigate(_config.Url);
        }
        else
        {
            _logger.Log($"[config] invalid Url: '{_config.Url}'");
            _overlay.ShowConfigError();
            _overlay.Visible = true;
            _overlay.BringToFront();
            _adminTouchTarget.BringToFront();
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _retry.ReportSuccess();
            _overlay.Visible = false;
            _logger.Log("[navigation] success");
            _ = ApplyAutoFitAsync();
        }
        else
        {
            _logger.Log("[navigation] failed -> retry");
            _overlay.ShowRetry();
            _overlay.Visible = true;
            _overlay.BringToFront();
            _adminTouchTarget.BringToFront();
            _retry.ReportFailure();
        }
    }

    private void OnRetry()
    {
        if (IsDisposed) return;
        if (_config.IsUrlValid && _webView.CoreWebView2 != null)
        {
            BeginInvoke(() => _webView.CoreWebView2.Navigate(_config.Url));
        }
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _logger.Log($"[webview2] process failed: {e.ProcessFailedKind}");
        _overlay.ShowGenericError("The web page rendering process crashed. Retrying…");
        _overlay.Visible = true;
        _overlay.BringToFront();
        _adminTouchTarget.BringToFront();
        _retry.ReportFailure();
        _ = ReinitAsync();
    }

    private async Task ReinitAsync()
    {
        try
        {
            await Task.Delay(2000);
            if (IsDisposed) return;
            if (_webView.CoreWebView2 == null)
                await _webView.EnsureCoreWebView2Async();
            NavigateOrShowConfigError();
        }
        catch (Exception ex)
        {
            _logger.Log($"[reinit] {ex}");
        }
    }

    // ---- Lock behavior ----

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // F4: intercept user-initiated closing (Alt+F4 / close button) unless authorized by an administrator.
        if (!_authorizedClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _logger.Log("[close] intercepted user close");
        }
    }

    private void OnDeactivate(object? sender, EventArgs e)
    {
        // F5: reclaim the foreground after losing focus, except while the admin dialog is open.
        if (TopMost && !_showingAdminDialog)
            BeginInvoke(() => Activate());
    }

    private void OnResize(object? sender, EventArgs e)
    {
        _ = ApplyAutoFitAsync();
    }

    // ---- Administrator escape path ----

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (KioskUnlockPolicy.ShouldUnlockDirectly(keyData, _config))
        {
            _logger.Log("[admin] direct hotkey unlock without password");
            EnterUnlockedMode();
            return true;
        }

        if (_adminCombo != Keys.None && keyData == _adminCombo)
        {
            ShowAdminDialog();
            return true; // Consume the keystroke.
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == AdminHotKeyId)
        {
            if (KioskUnlockPolicy.ShouldUnlockDirectly(_adminCombo, _config))
            {
                _logger.Log("[admin] direct global hotkey unlock without password");
                EnterUnlockedMode();
                return;
            }

            ShowAdminDialog();
            return;
        }

        base.WndProc(ref m);
    }

    private void OnAdminTouchHold(object? sender, EventArgs e)
    {
        if (!KioskUnlockPolicy.HasConfiguredAdminPassword(_config))
        {
            _logger.Log("[admin] touch unlock without password");
            EnterUnlockedMode();
            _authorizedClose = true;
            return;
        }

        ShowAdminDialog(useTouchKeyboard: true);
    }

    private void ShowAdminDialog(bool useTouchKeyboard = false)
    {
        if (_showingAdminDialog)
            return;

        _showingAdminDialog = true;
        try
        {
            using var dlg = new AdminDialog(_config, message => _logger.Log(message));
            if (useTouchKeyboard)
                dlg.Shown += (_, _) => dlg.BeginInvoke(() => ShowTouchKeyboard(dlg));
            dlg.TopMost = TopMost;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            switch (dlg.Result)
            {
                case AdminAction.Exit:
                    _logger.Log("[admin] exit requested");
                    _authorizedClose = true;
                    Application.Exit();
                    break;
                case AdminAction.Reload:
                    _logger.Log("[admin] reload requested");
                    ReloadConfigAndNavigate();
                    break;
                case AdminAction.DevTools:
                    _logger.Log("[admin] toggle devtools");
                    ToggleDevTools();
                    break;
                case AdminAction.Unlock:
                    _logger.Log("[admin] unlock mode");
                    EnterUnlockedMode();
                    break;
            }
        }
        finally
        {
            _showingAdminDialog = false;
        }
    }

    private void ShowTouchKeyboard(Form owner)
    {
        if (owner.IsDisposed || !owner.IsHandleCreated)
            return;

        try
        {
            if (!TouchKeyboard.TryShow(owner.Handle))
                throw new InvalidOperationException("Windows rejected the touch keyboard request.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or InvalidCastException or
            NotSupportedException or DllNotFoundException or EntryPointNotFoundException)
        {
            _logger.Log($"[admin] failed to show Windows touch keyboard: {ex}");
            MessageBox.Show(owner, "Could not open the Windows touch keyboard. Check Windows touch keyboard settings or use another keyboard.",
                "KioskWin", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ReloadConfigAndNavigate()
    {
        KioskConfig reloaded;
        try
        {
            reloaded = KioskConfig.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        }
        catch (Exception ex)
        {
            _logger.Log($"[reload] failed to read config, keeping previous: {ex}");
            return;
        }
        _config = reloaded;
        _adminCombo = KeyCombinationParser.Parse(_config.AdminKeyCombination);
        RegisterAdminHotKey();
        _logger.Log($"[reload] url={_config.Url}");
        NavigateOrShowConfigError();
    }

    private async Task ApplyAutoFitAsync()
    {
        if (!_config.AutoFitToWindow || IsDisposed || _webView.CoreWebView2 == null)
            return;

        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(WebViewAutoFitScript.Create());
        }
        catch (Exception ex)
        {
            _logger.Log($"[autofit] {ex}");
        }
    }

    private void RegisterAdminHotKey()
    {
        if (!IsHandleCreated)
            return;

        UnregisterAdminHotKey();

        if (_adminCombo == Keys.None ||
            !HotKeyRegistration.TryCreate(_adminCombo, out var hotKey))
            return;

        _adminHotKeyRegistered = RegisterHotKey(Handle, AdminHotKeyId, hotKey.Modifiers, hotKey.VirtualKey);
        if (!_adminHotKeyRegistered)
            _logger.Log($"[hotkey] register failed: {_config.AdminKeyCombination}");
    }

    private void UnregisterAdminHotKey()
    {
        if (!_adminHotKeyRegistered)
            return;

        UnregisterHotKey(Handle, AdminHotKeyId);
        _adminHotKeyRegistered = false;
    }

    private void ToggleDevTools()
    {
        if (_webView.CoreWebView2 == null) return;
        _devToolsEnabled = !_devToolsEnabled;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = _devToolsEnabled;
        if (_devToolsEnabled)
            _webView.CoreWebView2.OpenDevToolsWindow();
    }

    private void EnterUnlockedMode()
    {
        TopMost = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        WindowState = FormWindowState.Normal;
        Bounds = new Rectangle(50, 50, 1024, 768);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, HotKeyModifiers fsModifiers, uint vk);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
