using System.Drawing;
using System.Windows.Forms;
using KioskWin.Core;

namespace KioskWin.Forms;

public enum AdminAction
{
    None,
    Exit,
    Reload,
    DevTools,
    Unlock,
}

public sealed class AdminDialog : Form
{
    private readonly KioskConfig _config;
    private readonly Action<string> _logFailure;

    private readonly TextBox _passwordBox;
    private readonly Button _verifyButton;
    private readonly Button _exitButton;
    private readonly Button _reloadButton;
    private readonly Button _devToolsButton;
    private readonly Button _unlockButton;

    public AdminAction Result { get; private set; } = AdminAction.None;

    public AdminDialog(KioskConfig config, Action<string> logFailure)
    {
        _config = config;
        _logFailure = logFailure;

        Text = "KioskWin Administrator";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(360, 300);

        var lbl = new Label
        {
            Text = "Admin password:",
            Location = new Point(20, 25),
            AutoSize = true,
        };

        _passwordBox = new TextBox
        {
            Location = new Point(20, 50),
            Size = new Size(320, 25),
            PasswordChar = '*',
        };

        _verifyButton = new Button { Text = "Verify", Location = new Point(250, 85), Size = new Size(90, 30) };
        _verifyButton.Click += (_, _) => VerifyPassword();

        _exitButton = MakeActionButton("Exit", 20, 120, AdminAction.Exit);
        _reloadButton = MakeActionButton("Reload page", 190, 120, AdminAction.Reload);
        _devToolsButton = MakeActionButton("Open DevTools", 20, 165, AdminAction.DevTools);
        _unlockButton = MakeActionButton("Unlock", 190, 165, AdminAction.Unlock);

        var unlockLabel = new Label
        {
            Text = "(Unlock: temporarily switch to a movable window for maintenance.)",
            Location = new Point(20, 210),
            Size = new Size(320, 60),
            ForeColor = Color.Gray,
        };

        Controls.AddRange(new Control[]
        {
            lbl, _passwordBox, _verifyButton,
            _exitButton, _reloadButton, _devToolsButton, _unlockButton, unlockLabel,
        });

        SetActionButtonsEnabled(false);
        AcceptButton = _verifyButton;
    }

    private Button MakeActionButton(string text, int x, int y, AdminAction action)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(150, 35),
            Enabled = false,
        };
        btn.Click += (_, _) =>
        {
            Result = action;
            DialogResult = DialogResult.OK;
            Close();
        };
        return btn;
    }

    private void SetActionButtonsEnabled(bool enabled)
    {
        _exitButton.Enabled = enabled;
        _reloadButton.Enabled = enabled;
        _devToolsButton.Enabled = enabled;
        _unlockButton.Enabled = enabled;
    }

    private void VerifyPassword()
    {
        if (KioskUnlockPolicy.VerifyAdminPassword(_passwordBox.Text, _config))
        {
            SetActionButtonsEnabled(true);
            _passwordBox.Enabled = false;
            _verifyButton.Enabled = false;
            _ = MessageBox.Show(this, "Verification successful. Select an action.", "KioskWin",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            _logFailure("admin auth failed");
            MessageBox.Show(this, "Incorrect password.", "KioskWin",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _passwordBox.Clear();
        }
    }
}
