using System.Drawing;
using System.Windows.Forms;

namespace KioskWin.Forms;

public sealed class RetryOverlay : UserControl
{
    private readonly Label _label;

    public RetryOverlay()
    {
        BackColor = Color.FromArgb(30, 30, 30);
        Dock = DockStyle.Fill;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
        };

        _label = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        table.Controls.Add(_label, 0, 0);
        Controls.Add(table);
    }

    private void SetMessage(string title, string detail)
        => _label.Text = string.IsNullOrEmpty(detail) ? title : $"{title}\n\n{detail}";

    public void ShowRetry() => SetMessage("Retrying…", "Network connection failed. Retrying automatically.");
    public void ShowConfigError() => SetMessage("Configuration error", "The URL is invalid. Contact your administrator.");
    public void ShowRuntimeMissing() => SetMessage("WebView2 runtime missing", "WebView2 runtime was not detected. Contact your administrator to install it.");
    public void ShowGenericError(string? detail = null) => SetMessage("Program error", detail ?? "An unknown error occurred. Check the log.");
}
