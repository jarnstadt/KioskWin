using System.Runtime.InteropServices;

namespace KioskWin.Forms;

internal static class TouchKeyboard
{
    private const string InputPaneClass = "Windows.UI.ViewManagement.InputPane";

    public static bool TryShow(IntPtr windowHandle)
    {
        Marshal.ThrowExceptionForHR(RoInitialize(0));
        try
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(InputPaneClass, InputPaneClass.Length, out var className));
            try
            {
                var interopId = typeof(IInputPaneInterop).GUID;
                Marshal.ThrowExceptionForHR(RoGetActivationFactory(className, in interopId, out var factoryPointer));
                try
                {
                    var factory = (IInputPaneInterop)Marshal.GetObjectForIUnknown(factoryPointer);
                    try
                    {
                        var paneId = typeof(IInputPane2).GUID;
                        factory.GetForWindow(windowHandle, in paneId, out var pane);
                        try
                        {
                            return pane.TryShow();
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(pane);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(factory);
                    }
                }
                finally
                {
                    Marshal.Release(factoryPointer);
                }
            }
            finally
            {
                Marshal.ThrowExceptionForHR(WindowsDeleteString(className));
            }
        }
        finally
        {
            RoUninitialize();
        }
    }

    [ComImport, Guid("75CF2C57-9195-4931-8332-F0B409E916AF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IInputPaneInterop
    {
        void GetIids(out uint count, out IntPtr iids);
        void GetRuntimeClassName(out IntPtr className);
        void GetTrustLevel(out int trustLevel);
        void GetForWindow(IntPtr windowHandle, in Guid iid, [MarshalAs(UnmanagedType.Interface)] out IInputPane2 pane);
    }

    [ComImport, Guid("8A6B3F26-7090-4793-944C-C3F2CDE26276")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IInputPane2
    {
        void GetIids(out uint count, out IntPtr iids);
        void GetRuntimeClassName(out IntPtr className);
        void GetTrustLevel(out int trustLevel);
        [return: MarshalAs(UnmanagedType.U1)]
        bool TryShow();
    }

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string source, int length, out IntPtr value);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(IntPtr value);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(IntPtr className, in Guid iid, out IntPtr factory);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoInitialize(uint initType);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern void RoUninitialize();
}
