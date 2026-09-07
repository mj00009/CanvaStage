using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CanvaStage.Windows.Services;

/// <summary>Only used by isolated CI verification. Keep visuals attached to a live window so
/// bindings and loaded controls update normally, but allow it to exceed the runner desktop.</summary>
internal static class VerificationWindowSizing
{
    public static void Configure(Window window)
    {
        HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        source.AddHook(AllowLargeVerificationWindow);
        window.WindowState = WindowState.Normal;
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.Left = 0;
        window.Top = 0;
    }

    public static void CheckSize(Window window, double width, double height)
    {
        if (Math.Abs(window.ActualWidth - width) > 1 || Math.Abs(window.ActualHeight - height) > 1)
            throw new InvalidOperationException($"Verification window was constrained to {window.ActualWidth}x{window.ActualHeight}; expected {width}x{height}.");
    }

    private static nint AllowLargeVerificationWindow(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != 0x0024) return 0; // WM_GETMINMAXINFO
        MinMaxInfo bounds = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        bounds.MaxSize = new NativePoint { X = 8192, Y = 8192 };
        bounds.MaxTrackSize = bounds.MaxSize;
        Marshal.StructureToPtr(bounds, lParam, false);
        handled = true;
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }
}
