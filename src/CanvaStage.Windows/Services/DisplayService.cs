using System.Runtime.InteropServices;

namespace CanvaStage.Windows.Services;

public sealed record DisplayOption(string Id, string ScreenDeviceName, string FriendlyName, int Number, int Width, int Height, bool IsPrimary)
{
    public string Label => $"Display {Number} — {FriendlyName} — {Width} × {Height} ({(IsPrimary ? "Primary" : "Extended")})";
    public bool MatchesId(string? id) => string.Equals(Id, id, StringComparison.OrdinalIgnoreCase) || string.Equals(ScreenDeviceName, id, StringComparison.OrdinalIgnoreCase);
}

public sealed class DisplayService
{
    private const int DisplayDeviceActive = 0x00000001;

    public IReadOnlyList<DisplayOption> GetDisplays()
    {
        System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;
        var displays = new List<DisplayOption>(screens.Length);
        for (int index = 0; index < screens.Length; index++)
        {
            System.Windows.Forms.Screen screen = screens[index];
            DisplayIdentity identity = ReadIdentity(screen.DeviceName);
            displays.Add(new DisplayOption(
                string.IsNullOrWhiteSpace(identity.StableId) ? screen.DeviceName : identity.StableId,
                screen.DeviceName,
                NormalizeFriendlyName(identity.FriendlyName),
                index + 1,
                screen.Bounds.Width,
                screen.Bounds.Height,
                screen.Primary));
        }

        return displays;
    }

    public System.Windows.Forms.Screen? FindDisplay(string? id)
    {
        DisplayOption? option = GetDisplays().FirstOrDefault(display => display.MatchesId(id));
        string? deviceName = option?.ScreenDeviceName ?? id;
        return System.Windows.Forms.Screen.AllScreens.FirstOrDefault(screen => string.Equals(screen.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
    }

    private static DisplayIdentity ReadIdentity(string screenDeviceName)
    {
        var device = DisplayDevice.Create();
        for (uint index = 0; EnumDisplayDevices(screenDeviceName, index, ref device, 0); index++)
        {
            if ((device.StateFlags & DisplayDeviceActive) != 0 || index == 0)
            {
                string stableId = !string.IsNullOrWhiteSpace(device.DeviceId) ? device.DeviceId : device.DeviceKey;
                return new DisplayIdentity(stableId, device.DeviceString);
            }
            device = DisplayDevice.Create();
        }
        return new DisplayIdentity(screenDeviceName, "Display");
    }

    private static string NormalizeFriendlyName(string? name) => string.IsNullOrWhiteSpace(name) ? "Display" : name.Trim() switch
    {
        "Generic PnP Monitor" => "Monitor",
        "Generic Non-PnP Monitor" => "Monitor",
        string value => value
    };

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DisplayDevice lpDisplayDevice, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        public static DisplayDevice Create() => new() { Cb = Marshal.SizeOf<DisplayDevice>() };
    }

    private sealed record DisplayIdentity(string StableId, string FriendlyName);
}
