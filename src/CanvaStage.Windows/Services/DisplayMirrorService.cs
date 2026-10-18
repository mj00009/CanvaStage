using System.IO;
using System.Text.Json;

namespace CanvaStage.Windows.Services;

public sealed class DisplayMirrorService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true, WriteIndented = true };
    private readonly IDisplayTopologyBackend _backend;
    private readonly string _profilePath;
    public DisplayMirrorService(string? directory = null, IDisplayTopologyBackend? backend = null)
    {
        _backend = backend ?? new WindowsDisplayTopology();
        _profilePath = Path.Combine(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanvaStage"), "display-layout-restore.json");
    }
    public bool HasRestorePoint => File.Exists(_profilePath);
    public DisplayLayoutChange PrepareMirror(string audienceDevice, string confidenceDevice)
    {
        if (HasRestorePoint) throw new InvalidOperationException("A previous display layout is already saved. Restore that layout before mirroring again.");
        DisplayLayoutChange change = DisplayMirrorPolicy.Create(_backend.Capture(), audienceDevice, confidenceDevice);
        _backend.Validate(change.After);
        return change;
    }
    public DisplayLayoutChange PrepareRestore()
    {
        DisplayLayoutChange saved = ReadProfile();
        DisplayTopology current = _backend.Capture();
        if (!current.SameTargets(saved.Before) || !saved.Before.SamePrimary(current)
            || (current.ShapeKey != saved.After.ShapeKey && current.ShapeKey != saved.Before.ShapeKey))
            throw new InvalidOperationException("The screens or Windows layout have changed since the fallback. Reconnect the original screens, or restore Extend in Windows Display settings. CanvaStage will not guess another layout.");
        _backend.Validate(saved.Before);
        return new(current, saved.Before);
    }
    public void ApplyMirror(DisplayLayoutChange change)
    {
        RequireUnchanged(change.Before);
        SaveProfile(change); // Never switch without a durable restore point.
        bool attempted = false;
        try
        {
            RequireUnchanged(change.Before);
            attempted = true;
            _backend.Apply(change.After);
            DisplayTopology actual = _backend.Capture();
            if (!actual.SameTargets(change.Before) || !change.Before.SamePrimary(actual) || actual.ShapeKey != change.After.ShapeKey)
                throw new InvalidOperationException("Windows changed more of the desktop than requested. The previous layout will be restored.");
            // The saved proposal already has the verified shape. Keep the durable original
            // instead of replacing its recovery file after Windows has switched screens.
        }
        catch
        {
            if (attempted) RestoreAfterFailure(change.Before);
            else File.Delete(_profilePath);
            throw;
        }
    }
    public void ApplyRestore(DisplayLayoutChange change)
    {
        RequireUnchanged(change.Before);
        _backend.Apply(change.After);
        DisplayTopology actual = _backend.Capture();
        if (!actual.SameTargets(change.After) || actual.ShapeKey != change.After.ShapeKey)
            throw new InvalidOperationException("Windows has not restored the saved layout. Use Windows Display settings to check Extend; the restore point has been kept.");
        File.Delete(_profilePath);
    }
    public void RevertMirror()
    {
        DisplayLayoutChange saved = ReadProfile();
        RestoreAfterFailure(saved.Before);
    }
    private void RestoreAfterFailure(DisplayTopology original)
    {
        DisplayTopology current = _backend.Capture();
        if (!current.SameTargets(original))
            throw new InvalidOperationException("A screen disconnected before the layout could be restored. Reconnect it and choose Restore extended layout, or use Windows Display settings. The restore point has been kept.");
        _backend.Validate(original);
        _backend.Apply(original);
        if (_backend.Capture().ShapeKey != original.ShapeKey)
            throw new InvalidOperationException("Windows could not restore the saved layout. Use Windows Display settings; the restore point has been kept.");
        File.Delete(_profilePath);
    }
    public void RequireUnchanged(DisplayTopology expected)
    {
        if (_backend.Capture().LayoutKey != expected.LayoutKey)
            throw new InvalidOperationException("The display layout changed while confirmation was open. No requested change was applied. Check the screens and try again.");
    }
    private DisplayLayoutChange ReadProfile()
    {
        if (!File.Exists(_profilePath) || new FileInfo(_profilePath).Length > 1024 * 1024)
            throw new InvalidOperationException("No usable saved display layout is available. Use Windows Display settings to restore Extend.");
        DisplayLayoutChange saved = JsonSerializer.Deserialize<DisplayLayoutChange>(File.ReadAllText(_profilePath), JsonOptions)
            ?? throw new InvalidOperationException("The saved display layout could not be read. Use Windows Display settings to restore Extend.");
        if (!saved.Before.SameTargets(saved.After) || !saved.Before.SamePrimary(saved.After)
            || saved.Before.Paths.Length > 32 || saved.Before.Modes.Length > 256)
            throw new InvalidOperationException("The saved display layout is invalid. Use Windows Display settings to restore Extend.");
        return saved;
    }
    private void SaveProfile(DisplayLayoutChange profile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_profilePath)!);
        string temporary = _profilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(profile, JsonOptions));
        File.Move(temporary, _profilePath); // Never overwrite another pending restore point.
    }
}
