using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CanvaStage.Windows.Services;

public interface IDisplayTopologyBackend
{
    DisplayTopology Capture();
    void Validate(DisplayTopology topology);
    void Apply(DisplayTopology topology);
}

public sealed class WindowsDisplayTopology : IDisplayTopologyBackend
{
    private const uint QueryFlags = 2 | 0x10; // Active paths, virtual-mode aware.
    private const uint SetFlags = 0x20 | 0x400 | 0x8000; // Supplied modes, compatible adjustments, virtual-mode aware.

    public DisplayTopology Capture()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Check(GetDisplayConfigBufferSizes(QueryFlags, out uint pathCount, out uint modeCount));
            if (pathCount is 0 or > 32 || modeCount > 256) throw new InvalidOperationException("Windows returned an unsupported display configuration.");
            DisplayPath[] paths = new DisplayPath[pathCount];
            DisplayMode[] modes = new DisplayMode[modeCount];
            int result = QueryDisplayConfig(QueryFlags, ref pathCount, paths, ref modeCount, modes, 0);
            if (result == 122) continue; // A monitor changed between sizing and querying.
            Check(result);
            Array.Resize(ref paths, checked((int)pathCount));
            Array.Resize(ref modes, checked((int)modeCount));
            DisplayEndpoint[] endpoints = paths.Select(path =>
            {
                SourceName source = new() { Header = new() { Type = 1, Size = (uint)Marshal.SizeOf<SourceName>(), Adapter = path.Source.Adapter, Id = path.Source.Id } };
                TargetName target = new() { Header = new() { Type = 2, Size = (uint)Marshal.SizeOf<TargetName>(), Adapter = path.Target.Adapter, Id = path.Target.Id } };
                Check(DisplayConfigGetDeviceInfo(ref source));
                Check(DisplayConfigGetDeviceInfo(ref target));
                if (string.IsNullOrWhiteSpace(target.MonitorPath)) throw new InvalidOperationException("Windows could not identify a physical screen. No layout change was made.");
                return new DisplayEndpoint(DisplayTopology.TargetKey(path), source.Name, target.MonitorPath);
            }).ToArray();
            return new(paths, modes, endpoints);
        }
        throw new InvalidOperationException("The display connection is still changing. Wait a moment, refresh displays and try again.");
    }

    public void Validate(DisplayTopology topology) => Set(topology, 0x40);
    public void Apply(DisplayTopology topology) => Set(topology, 0x80 | 0x200);
    private static void Set(DisplayTopology topology, uint action)
    {
        // Windows may adjust the arrays. Never mutate the saved restore point.
        DisplayPath[] paths = (DisplayPath[])topology.Paths.Clone();
        DisplayMode[] modes = (DisplayMode[])topology.Modes.Clone();
        Check(SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes, SetFlags | action));
    }
    private static void Check(int result)
    {
        if (result != 0) throw new Win32Exception(result, $"Windows could not use that display layout (error {result}). Check that all screens are connected and the graphics driver supports mirroring.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Header { public uint Type, Size; public DisplayLuid Adapter; public uint Id; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SourceName { public Header Header; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TargetName
    {
        public Header Header;
        public uint Flags, Technology;
        public ushort Manufacturer, Product;
        public uint Connector;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string FriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string MonitorPath;
    }
    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint pathCount, [Out] DisplayPath[] paths, ref uint modeCount, [Out] DisplayMode[] modes, nint topologyId);
    [DllImport("user32.dll")] private static extern int SetDisplayConfig(uint pathCount, [In, Out] DisplayPath[] paths, uint modeCount, [In, Out] DisplayMode[] modes, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DisplayConfigGetDeviceInfo(ref SourceName name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DisplayConfigGetDeviceInfo(ref TargetName name);
}
