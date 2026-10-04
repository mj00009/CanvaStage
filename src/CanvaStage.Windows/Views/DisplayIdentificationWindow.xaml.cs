using System.Windows;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using CanvaStage.Windows.Services;

namespace CanvaStage.Windows.Views;

public partial class DisplayIdentificationWindow : Window
{
    public DisplayIdentificationWindow(DisplayOptionViewModel display)
    {
        InitializeComponent();
        DataContext = display;
    }

    internal void ShowOnDisplay(Rectangle monitor)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        nint handle = new WindowInteropHelper(this).EnsureHandle();
        // First move the hidden HWND to acquire the target monitor's DPI.
        // Left/Top are WPF units; monitor bounds are physical desktop pixels.
        SetNativeBounds(handle, monitor, 0x0015); // No resize, no activation, no z-order change.
        double scale = Math.Max(96, GetDpiForWindow(handle)) / 96d;
        Rectangle bounds = DisplayIdentificationPlacement.GetBounds(monitor, scale);
        Width = bounds.Width / scale;
        Height = bounds.Height / scale;
        Show();
        SetNativeBounds(handle, bounds, 0x0010); // Topmost without stealing focus.
        PrivacySafeLog.Write(FormattableString.Invariant($"Display identification shown: number {((DisplayOptionViewModel)DataContext).Number}; dpi {scale:0.00}; target {bounds}; actual {GetNativeBounds()}."));
    }

    internal Rectangle GetNativeBounds()
    {
        nint handle = new WindowInteropHelper(this).Handle;
        if (handle == 0 || !GetWindowRect(handle, out NativeRect bounds)) return Rectangle.Empty;
        return Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }

    private static void SetNativeBounds(nint handle, Rectangle bounds, uint flags)
    {
        if (!SetWindowPos(handle, new nint(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, flags))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not position a display identification label.");
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out NativeRect bounds);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
}

public sealed record DisplayOptionViewModel(int Number, string FriendlyName, string Resolution);
