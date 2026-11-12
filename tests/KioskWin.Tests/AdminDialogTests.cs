using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using KioskWin.Core;
using KioskWin.Forms;
using Xunit;

namespace KioskWin.Tests;

public class AdminDialogTests
{
    [Theory]
    [InlineData("")]
    [InlineData("example-password")]
    public void Reload_actions_are_available_without_authentication(string password)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var dialog = new AdminDialog(new KioskConfig { AdminPassword = password }, _ => { });
                var buttons = dialog.Controls.OfType<Button>().ToDictionary(button => button.Text);

                Assert.True(buttons["Reload page"].Enabled);
                Assert.True(buttons["Clear cache and reload"].Enabled);
                Assert.False(buttons["Exit"].Enabled);
                Assert.False(buttons["Open DevTools"].Enabled);
                Assert.False(buttons["Unlock"].Enabled);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
