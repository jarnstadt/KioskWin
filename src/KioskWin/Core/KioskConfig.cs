using Microsoft.Extensions.Configuration;

namespace KioskWin.Core;

public sealed class KioskConfig
{
    public string Url { get; set; } = "";
    public string AdminKeyCombination { get; set; } = "Ctrl+Shift+Alt+Q";
    public string AdminPasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public int RetryIntervalSeconds { get; set; } = 10;
    public bool TopMost { get; set; } = true;
    public bool ShowInTaskbar { get; set; } = false;
    public string UserDataFolder { get; set; } = "WebView2Data";
    public bool AutoFitToWindow { get; set; } = true;

    public bool IsUrlValid =>
        Uri.TryCreate(Url, UriKind.Absolute, out var u)
        && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    public TimeSpan RetryInterval => TimeSpan.FromSeconds(Math.Clamp(RetryIntervalSeconds, 1, 600));

    public static KioskConfig Load(IConfiguration configuration)
    {
        var cfg = new KioskConfig();
        configuration.Bind(cfg);

        // Bind overwrites the defaults above with JSON values; normalize invalid or empty values here.
        if (string.IsNullOrWhiteSpace(cfg.AdminKeyCombination))
            cfg.AdminKeyCombination = "Ctrl+Shift+Alt+Q";
        if (string.IsNullOrWhiteSpace(cfg.UserDataFolder))
            cfg.UserDataFolder = "WebView2Data";
        if (cfg.RetryIntervalSeconds <= 0)
            cfg.RetryIntervalSeconds = 10;
        cfg.RetryIntervalSeconds = Math.Clamp(cfg.RetryIntervalSeconds, 1, 600);

        return cfg;
    }

    public static KioskConfig LoadFromFile(string jsonFilePath)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile(jsonFilePath, optional: true, reloadOnChange: false);
        return Load(builder.Build());
    }
}
