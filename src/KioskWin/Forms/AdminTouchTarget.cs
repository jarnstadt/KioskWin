using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KioskWin.Forms;

internal sealed class AdminTouchTarget : Control
{
    private const int WmTouch = 0x0240;
    private const uint TouchMove = 0x0001;
    private const uint TouchDown = 0x0002;
    private const uint TouchUp = 0x0004;
    private readonly System.Windows.Forms.Timer _holdTimer = new() { Interval = 3000 };
    private readonly Action<string> _logFailure;
    private uint? _touchId;

    public event EventHandler? HoldCompleted;

    public AdminTouchTarget(Action<string> logFailure)
    {
        _logFailure = logFailure;
        Size = new Size(48, 48);
        BackColor = Color.FromArgb(30, 30, 30);
        Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _holdTimer.Tick += (_, _) =>
        {
            CancelHold();
            HoldCompleted?.Invoke(this, EventArgs.Empty);
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!RegisterTouchWindow(Handle, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to register the admin touch target.");
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        CancelHold();
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _holdTimer.Dispose();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg != WmTouch)
        {
            base.WndProc(ref m);
            return;
        }

        try
        {
            var count = (int)(m.WParam.ToInt64() & 0xFFFF);
            var inputs = new TouchInput[count];
            if (!GetTouchInputInfo(m.LParam, count, inputs, Marshal.SizeOf<TouchInput>()))
            {
                CancelHold();
                _logFailure($"[touch] GetTouchInputInfo failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                return;
            }

            foreach (var input in inputs)
                ProcessTouch(input);
        }
        finally
        {
            if (!CloseTouchInputHandle(m.LParam))
                _logFailure($"[touch] CloseTouchInputHandle failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }
    }

    private void ProcessTouch(TouchInput input)
    {
        if ((input.Flags & TouchDown) != 0)
        {
            if (_touchId.HasValue)
            {
                CancelHold();
                return;
            }

            _touchId = input.Id;
            _holdTimer.Start();
            return;
        }

        if (input.Id != _touchId)
            return;

        if ((input.Flags & TouchUp) != 0 ||
            ((input.Flags & TouchMove) != 0 &&
             !ClientRectangle.Contains(PointToClient(new Point(input.X / 100, input.Y / 100)))))
            CancelHold();
    }

    private void CancelHold()
    {
        _holdTimer.Stop();
        _touchId = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TouchInput
    {
        public int X;
        public int Y;
        public IntPtr Source;
        public uint Id;
        public uint Flags;
        public uint Mask;
        public uint Time;
        public UIntPtr ExtraInfo;
        public uint ContactWidth;
        public uint ContactHeight;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterTouchWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetTouchInputInfo(IntPtr touchInputHandle, int inputCount,
        [Out] TouchInput[] inputs, int inputSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseTouchInputHandle(IntPtr touchInputHandle);
}
