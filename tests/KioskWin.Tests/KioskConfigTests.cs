using Microsoft.Extensions.Configuration;
using KioskWin.Core;
using System.Windows.Forms;
using Xunit;

namespace KioskWin.Tests;

public class KioskConfigTests
{
    private static IConfiguration ConfigFromMemory(params (string Key, string Value)[] pairs)
    {
        var dict = pairs.ToDictionary(p => p.Key, p => (string?)p.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void Loads_all_fields()
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(
            ("Url", "https://example.com/"),
            ("AdminKeyCombination", "Ctrl+Alt+P"),
            ("AdminPasswordHash", "ABCD"),
            ("PasswordSalt", "1234"),
            ("RetryIntervalSeconds", "30"),
            ("TopMost", "false"),
            ("ShowInTaskbar", "true"),
            ("UserDataFolder", "Data"),
            ("AutoFitToWindow", "false")
        ));

        Assert.Equal("https://example.com/", cfg.Url);
        Assert.Equal("Ctrl+Alt+P", cfg.AdminKeyCombination);
        Assert.Equal("ABCD", cfg.AdminPasswordHash);
        Assert.Equal("1234", cfg.PasswordSalt);
        Assert.Equal(30, cfg.RetryIntervalSeconds);
        Assert.False(cfg.TopMost);
        Assert.True(cfg.ShowInTaskbar);
        Assert.Equal("Data", cfg.UserDataFolder);
        Assert.False(cfg.AutoFitToWindow);
    }

    [Fact]
    public void IsUrlValid_true_for_https()
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(("Url", "https://position.lmding.cn/x")));
        Assert.True(cfg.IsUrlValid);
    }

    [Fact]
    public void IsUrlValid_false_for_relative()
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(("Url", "not a url")));
        Assert.False(cfg.IsUrlValid);
        // Preserve an invalid URL as-is.
        Assert.Equal("not a url", cfg.Url);
    }

    [Fact]
    public void Defaults_when_fields_missing()
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(("Url", "https://x")));
        Assert.Equal("Ctrl+Shift+Alt+Q", cfg.AdminKeyCombination);
        Assert.Equal("WebView2Data", cfg.UserDataFolder);
        Assert.Equal(10, cfg.RetryIntervalSeconds);
        Assert.True(cfg.TopMost);
        Assert.False(cfg.ShowInTaskbar);
        Assert.True(cfg.AutoFitToWindow);
    }

    [Fact]
    public void Appsettings_defaults_to_baidu_url()
    {
        var root = FindRepositoryRoot();
        var cfg = KioskConfig.LoadFromFile(Path.Combine(root, "src", "KioskWin", "appsettings.json"));

        Assert.Equal("https://www.baidu.com", cfg.Url);
    }

    [Fact]
    public void Configured_admin_hotkey_directly_unlocks_when_admin_password_is_not_configured()
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(
            ("AdminPasswordHash", ""),
            ("PasswordSalt", "")));
        var keyData = KeyCombinationParser.Parse(cfg.AdminKeyCombination);

        Assert.True(KioskUnlockPolicy.ShouldUnlockDirectly(keyData, cfg));
    }

    [Theory]
    [InlineData("", "salt")]
    [InlineData("hash", "")]
    public void Configured_admin_hotkey_directly_unlocks_when_admin_password_is_incomplete(string hash, string salt)
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(
            ("AdminPasswordHash", hash),
            ("PasswordSalt", salt)));
        var keyData = KeyCombinationParser.Parse(cfg.AdminKeyCombination);

        Assert.True(KioskUnlockPolicy.ShouldUnlockDirectly(keyData, cfg));
    }

    [Theory]
    [InlineData(Keys.Enter)]
    [InlineData(Keys.F1)]
    [InlineData(Keys.Escape)]
    public void Non_admin_hotkeys_do_not_directly_unlock_without_admin_password(Keys keyData)
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(
            ("AdminPasswordHash", ""),
            ("PasswordSalt", "")));

        Assert.False(KioskUnlockPolicy.ShouldUnlockDirectly(keyData, cfg));
    }

    [Fact]
    public void Configured_admin_hotkey_does_not_directly_unlock_when_admin_password_is_configured()
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(
            ("AdminPasswordHash", "hash"),
            ("PasswordSalt", "salt")));
        var keyData = KeyCombinationParser.Parse(cfg.AdminKeyCombination);

        Assert.False(KioskUnlockPolicy.ShouldUnlockDirectly(keyData, cfg));
    }

    [Theory]
    [InlineData(0, 10)]      // Invalid -> default to 10
    [InlineData(-5, 10)]     // Invalid -> default to 10
    [InlineData(3, 3)]
    [InlineData(99999, 600)] // Upper limit
    public void RetryInterval_clamped(int input, int expectedSeconds)
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(
            ("Url", "https://x"),
            ("RetryIntervalSeconds", input.ToString())));
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), cfg.RetryInterval);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KioskWin.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
