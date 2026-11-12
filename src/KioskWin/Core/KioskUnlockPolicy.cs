using System.Windows.Forms;

namespace KioskWin.Core;

public static class KioskUnlockPolicy
{
    public static bool ShouldUnlockDirectly(Keys keyData, KioskConfig config)
    {
        var adminCombo = KeyCombinationParser.Parse(config.AdminKeyCombination);
        return adminCombo != Keys.None &&
            keyData == adminCombo &&
            !HasConfiguredAdminPassword(config);
    }

    public static bool HasConfiguredAdminPassword(KioskConfig config)
    {
        return !string.IsNullOrWhiteSpace(config.AdminPassword);
    }

    public static bool VerifyAdminPassword(string password, KioskConfig config) =>
        HasConfiguredAdminPassword(config) &&
        string.Equals(password, config.AdminPassword, StringComparison.Ordinal);
}
