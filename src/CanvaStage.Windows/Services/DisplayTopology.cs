using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace CanvaStage.Windows.Services;

// CCD structures use the Windows ABI even in the pure policy tests.
[StructLayout(LayoutKind.Sequential)]
public struct DisplayLuid { public uint Low; public int High; }
[StructLayout(LayoutKind.Sequential)]
public struct DisplaySource { public DisplayLuid Adapter; public uint Id, ModeIndex, Status; }
[StructLayout(LayoutKind.Sequential)]
public struct DisplayRational { public uint Numerator, Denominator; }
[StructLayout(LayoutKind.Sequential)]
public struct DisplayTarget
{
    public DisplayLuid Adapter;
    public uint Id, ModeIndex, Technology, Rotation, Scaling;
    public DisplayRational Refresh;
    public uint ScanLine;
    public int Available;
    public uint Status;
}
[StructLayout(LayoutKind.Sequential)]
public struct DisplayPath { public DisplaySource Source; public DisplayTarget Target; public uint Flags; }
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct DisplayMode
{
    [FieldOffset(0)] public uint Type;
    [FieldOffset(4)] public uint Id;
    [FieldOffset(8)] public DisplayLuid Adapter;
    [FieldOffset(16)] public ulong Data0;
    [FieldOffset(24)] public ulong Data1;
    [FieldOffset(32)] public ulong Data2;
    [FieldOffset(40)] public ulong Data3;
    [FieldOffset(48)] public ulong Data4;
    [FieldOffset(56)] public ulong Data5;
    [JsonIgnore, FieldOffset(16)] public uint Width;
    [JsonIgnore, FieldOffset(20)] public uint Height;
    [JsonIgnore, FieldOffset(24)] public uint PixelFormat;
    [JsonIgnore, FieldOffset(28)] public int X;
    [JsonIgnore, FieldOffset(32)] public int Y;
}

public sealed record DisplayEndpoint(string Key, string DeviceName, string MonitorPath);
public sealed record DisplayTopology(DisplayPath[] Paths, DisplayMode[] Modes, DisplayEndpoint[] Endpoints)
{
    public static string TargetKey(DisplayPath path) => $"{path.Target.Adapter.High}:{path.Target.Adapter.Low}:{path.Target.Id}";
    public int SourceIndex(DisplayPath path) => checked((int)((path.Flags & 8) != 0 ? path.Source.ModeIndex >> 16 : path.Source.ModeIndex));
    public DisplayMode SourceMode(DisplayPath path)
    {
        int index = SourceIndex(path);
        if (index < 0 || index >= Modes.Length || Modes[index].Type != 1)
            throw new InvalidOperationException("Windows did not provide a usable display mode. Refresh displays and try again.");
        return Modes[index];
    }
    public DisplayPath FindDevice(string device)
    {
        DisplayEndpoint[] matches = Endpoints.Where(endpoint => string.Equals(endpoint.DeviceName, device, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("The chosen screen is missing or already mirrored. Use three Extended screens before choosing this fallback.");
        return Paths.Single(path => TargetKey(path) == matches[0].Key);
    }
    public string LayoutKey => string.Join("|", Paths.OrderBy(TargetKey).Select(path =>
    {
        DisplayMode mode = SourceMode(path);
        string monitor = Endpoints.Single(endpoint => endpoint.Key == TargetKey(path)).MonitorPath;
        return $"{TargetKey(path)}:{monitor}:{path.Source.Adapter.High}:{path.Source.Adapter.Low}:{path.Source.Id}:{mode.X}:{mode.Y}:{mode.Width}:{mode.Height}:{path.Target.Rotation}:{path.Target.Scaling}";
    }));
    public bool SameTargets(DisplayTopology other) => Endpoints.Select(endpoint => (endpoint.Key, endpoint.MonitorPath)).OrderBy(item => item.Key)
        .SequenceEqual(other.Endpoints.Select(endpoint => (endpoint.Key, endpoint.MonitorPath)).OrderBy(item => item.Key));
    public string ShapeKey => string.Join("|", Paths.OrderBy(TargetKey).Select(path =>
    {
        DisplayMode mode = SourceMode(path);
        return $"{TargetKey(path)}:{mode.X}:{mode.Y}:{mode.Width}:{mode.Height}:{path.Target.Rotation}";
    }));
    public bool SamePrimary(DisplayTopology other)
    {
        DisplayPath[] primary = Paths.Where(path => SourceMode(path) is { X: 0, Y: 0 }).ToArray();
        if (primary.Length != 1) return false;
        DisplayPath? match = other.Paths.Cast<DisplayPath?>().SingleOrDefault(path => TargetKey(path!.Value) == TargetKey(primary[0]));
        return match is { } current && other.SourceMode(current) is { X: 0, Y: 0 } mode
            && mode.Width == SourceMode(primary[0]).Width && mode.Height == SourceMode(primary[0]).Height;
    }
}

public sealed record DisplayLayoutChange(DisplayTopology Before, DisplayTopology After);

public static class DisplayMirrorPolicy
{
    public static DisplayLayoutChange Create(DisplayTopology before, string audienceDevice, string confidenceDevice)
    {
        if (before.Paths.Length < 3 || !before.Paths.All(path => (path.Flags & 1) != 0))
            throw new InvalidOperationException("Connect three screens using Extend first. This fallback cannot reconnect a missing screen.");
        DisplayPath source = before.FindDevice(audienceDevice);
        DisplayPath target = before.FindDevice(confidenceDevice);
        DisplayMode sourceMode = before.SourceMode(source);
        DisplayMode targetMode = before.SourceMode(target);
        if (DisplayTopology.TargetKey(source) == DisplayTopology.TargetKey(target)
            || sourceMode is { X: 0, Y: 0 } || targetMode is { X: 0, Y: 0 })
            throw new InvalidOperationException("Choose two different Extended screens. The primary operator screen must stay independent.");
        if (before.Paths.GroupBy(path => (before.SourceMode(path).X, before.SourceMode(path).Y)).Any(group => group.Count() != 1))
            throw new InvalidOperationException("The desktop already includes mirrored screens. Restore Extend in Windows before setting up this fallback.");
        if ((source.Flags & 8) != (target.Flags & 8))
            throw new InvalidOperationException("These screens do not support the same Windows mirroring mode. Use Windows Display settings to check compatible layouts.");
        DisplayPath[] paths = (DisplayPath[])before.Paths.Clone();
        DisplayMode[] modes = (DisplayMode[])before.Modes.Clone();
        int targetIndex = Array.FindIndex(paths, path => DisplayTopology.TargetKey(path) == DisplayTopology.TargetKey(target));
        if ((source.Flags & 8) != 0)
        {
            int modeIndex = before.SourceIndex(target);
            modes[modeIndex].Width = sourceMode.Width;
            modes[modeIndex].Height = sourceMode.Height;
            modes[modeIndex].PixelFormat = sourceMode.PixelFormat;
            modes[modeIndex].X = sourceMode.X;
            modes[modeIndex].Y = sourceMode.Y;
            paths[targetIndex].Target.ModeIndex |= 0xFFFF; // Invalidate desktop-image index, retain target timing.
        }
        else
        {
            if (!source.Source.Adapter.Equals(target.Source.Adapter))
                throw new InvalidOperationException("These screens are on different graphics adapters and do not support virtual mirroring.");
            paths[targetIndex].Source.Id = source.Source.Id;
            paths[targetIndex].Source.ModeIndex = source.Source.ModeIndex;
            paths[targetIndex].Target.ModeIndex = uint.MaxValue;
        }
        DisplayTopology after = new(paths, modes, before.Endpoints);
        if (!before.SamePrimary(after)) throw new InvalidOperationException("The operator screen could not be preserved. No display change was made.");
        return new(before, after);
    }
}
