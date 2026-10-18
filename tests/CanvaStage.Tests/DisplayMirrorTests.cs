using CanvaStage.Windows.Services;
using System.Runtime.InteropServices;
using Xunit;

namespace CanvaStage.Tests;

public sealed class DisplayMirrorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "canvastage-display-test-" + Guid.NewGuid().ToString("N"));
    private readonly Backend _backend = new();
    private DisplayMirrorService Create() => new(_directory, _backend);

    [Fact]
    public void NativeStructuresHaveWindowsSizes()
    {
        Assert.Equal(20, Marshal.SizeOf<DisplaySource>());
        Assert.Equal(48, Marshal.SizeOf<DisplayTarget>());
        Assert.Equal(72, Marshal.SizeOf<DisplayPath>());
        Assert.Equal(64, Marshal.SizeOf<DisplayMode>());
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MirrorsOnlyRequestedPairWithoutMutatingSavedLayout(bool virtualMode)
    {
        DisplayTopology before = Sample(virtualMode);
        string original = before.LayoutKey;
        DisplayLayoutChange change = DisplayMirrorPolicy.Create(before, "DISPLAY2", "DISPLAY3");
        Assert.True(before.SamePrimary(change.After));
        Assert.True(before.SameTargets(change.After));
        Assert.Equal(change.After.SourceMode(change.After.Paths[1]).X, change.After.SourceMode(change.After.Paths[2]).X);
        Assert.Equal(3840, before.SourceMode(before.Paths[2]).X);
        Assert.Equal(original, before.LayoutKey);
    }
    [Theory]
    [InlineData("DISPLAY1", "DISPLAY3")]
    [InlineData("DISPLAY2", "DISPLAY1")]
    [InlineData("DISPLAY2", "DISPLAY2")]
    [InlineData("missing", "DISPLAY3")]
    public void RejectsPrimarySameOrMissingTargets(string source, string target) =>
        Assert.Throws<InvalidOperationException>(() => DisplayMirrorPolicy.Create(Sample(), source, target));

    [Fact]
    public void RejectsAmbiguousAlreadyClonedAndMixedVirtualModes()
    {
        DisplayTopology sample = Sample();
        DisplayEndpoint[] endpoints = (DisplayEndpoint[])sample.Endpoints.Clone();
        endpoints[2] = endpoints[2] with { DeviceName = "DISPLAY2" };
        Assert.Throws<InvalidOperationException>(() => DisplayMirrorPolicy.Create(sample with { Endpoints = endpoints }, "DISPLAY2", "DISPLAY3"));
        DisplayTopology cloned = DisplayMirrorPolicy.Create(sample, "DISPLAY2", "DISPLAY3").After;
        Assert.Throws<InvalidOperationException>(() => DisplayMirrorPolicy.Create(cloned, "DISPLAY2", "DISPLAY3"));
        sample.Paths[2].Flags &= ~8u;
        Assert.Throws<InvalidOperationException>(() => DisplayMirrorPolicy.Create(sample, "DISPLAY2", "DISPLAY3"));
    }
    [Fact]
    public void RejectsLegacyCrossAdapterClone()
    {
        DisplayTopology sample = Sample(false);
        sample.Paths[2].Source.Adapter.Low = 2;
        Assert.Throws<InvalidOperationException>(() => DisplayMirrorPolicy.Create(sample, "DISPLAY2", "DISPLAY3"));
    }
    [Fact]
    public void RejectsTwoScreens()
    {
        DisplayTopology sample = Sample();
        Assert.Throws<InvalidOperationException>(() => DisplayMirrorPolicy.Create(sample with { Paths = sample.Paths[..2], Endpoints = sample.Endpoints[..2] }, "DISPLAY2", "DISPLAY3"));
    }
    [Fact]
    public void SavedLayoutSurvivesAppRestartAndRestoresExactExtendedGeometry()
    {
        DisplayMirrorService service = Create();
        string original = _backend.Current.LayoutKey;
        DisplayLayoutChange change = service.PrepareMirror("DISPLAY2", "DISPLAY3");
        service.ApplyMirror(change);
        DisplayMirrorService reopened = Create();
        Assert.True(reopened.HasRestorePoint);
        reopened.ApplyRestore(reopened.PrepareRestore());
        Assert.Equal(original, _backend.Current.LayoutKey);
        Assert.False(reopened.HasRestorePoint);
    }
    [Fact]
    public void StaleConsentDoesNotApplyAnything()
    {
        DisplayMirrorService service = Create();
        DisplayLayoutChange change = service.PrepareMirror("DISPLAY2", "DISPLAY3");
        _backend.Current.Modes[2].X += 100;
        Assert.Throws<InvalidOperationException>(() => service.ApplyMirror(change));
        Assert.Equal(0, _backend.Applies);
        Assert.False(service.HasRestorePoint);
    }
    [Fact]
    public void CannotSwitchWithoutDurableRestorePoint()
    {
        File.WriteAllText(_directory, "Block directory creation");
        DisplayMirrorService service = Create();
        DisplayLayoutChange change = service.PrepareMirror("DISPLAY2", "DISPLAY3");
        Assert.ThrowsAny<IOException>(() => service.ApplyMirror(change));
        Assert.Equal(0, _backend.Applies);
    }
    [Fact]
    public void FailedApplyRollsBackAndReleasesRestorePoint()
    {
        DisplayMirrorService service = Create();
        DisplayLayoutChange change = service.PrepareMirror("DISPLAY2", "DISPLAY3");
        _backend.FailNextApply = true;
        Assert.Throws<InvalidOperationException>(() => service.ApplyMirror(change));
        Assert.Equal(2, _backend.Applies);
        Assert.Equal(change.Before.ShapeKey, _backend.Current.ShapeKey);
        Assert.False(service.HasRestorePoint);
    }
    [Fact]
    public void DeclinedOrTimedOutKeepRestoresSavedLayout()
    {
        DisplayMirrorService service = Create();
        DisplayLayoutChange change = service.PrepareMirror("DISPLAY2", "DISPLAY3");
        service.ApplyMirror(change);
        service.RevertMirror();
        Assert.Equal(change.Before.ShapeKey, _backend.Current.ShapeKey);
        Assert.False(service.HasRestorePoint);
    }
    [Fact]
    public void HotplugOrManualLayoutChangeCannotRedirectRestore()
    {
        DisplayMirrorService service = Create();
        service.ApplyMirror(service.PrepareMirror("DISPLAY2", "DISPLAY3"));
        _backend.Current.Modes[0].Width = 1280;
        Assert.Throws<InvalidOperationException>(() => service.PrepareRestore());
        Assert.Equal(1, _backend.Applies);
        Assert.True(service.HasRestorePoint);
    }
    [Fact]
    public void InvalidRestorePointDoesNotInvokeWindows()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "display-layout-restore.json"), "invalid json");
        Assert.ThrowsAny<Exception>(() => Create().PrepareRestore());
        Assert.Equal(0, _backend.Applies);
    }
    [Fact]
    public void DisconnectedTargetKeepsRestorePointWithoutApplyingAnotherLayout()
    {
        DisplayMirrorService service = Create();
        service.ApplyMirror(service.PrepareMirror("DISPLAY2", "DISPLAY3"));
        _backend.Current = _backend.Current with { Paths = _backend.Current.Paths[..2], Endpoints = _backend.Current.Endpoints[..2] };
        Assert.Throws<InvalidOperationException>(() => service.RevertMirror());
        Assert.Equal(1, _backend.Applies);
        Assert.True(service.HasRestorePoint);
    }
    [Fact]
    public void FailedRollbackRetainsRestorePointForLaterRetry()
    {
        DisplayMirrorService service = Create();
        service.ApplyMirror(service.PrepareMirror("DISPLAY2", "DISPLAY3"));
        _backend.FailNextApply = true;
        Assert.Throws<InvalidOperationException>(() => service.RevertMirror());
        Assert.True(service.HasRestorePoint);
        service.ApplyRestore(service.PrepareRestore());
        Assert.False(service.HasRestorePoint);
    }
    [Fact]
    public void UnexpectedOperatorScreenAdjustmentRollsBack()
    {
        DisplayMirrorService service = Create();
        DisplayLayoutChange change = service.PrepareMirror("DISPLAY2", "DISPLAY3");
        _backend.AdjustNextApply = true;
        Assert.Throws<InvalidOperationException>(() => service.ApplyMirror(change));
        Assert.Equal(2, _backend.Applies);
        Assert.Equal(change.Before.ShapeKey, _backend.Current.ShapeKey);
        Assert.False(service.HasRestorePoint);
    }
    private static DisplayTopology Sample(bool virtualMode = true)
    {
        DisplayLuid adapter = new() { Low = 1 };
        DisplayPath[] paths = Enumerable.Range(0, 3).Select(index => new DisplayPath
        {
            Source = new() { Adapter = adapter, Id = (uint)index, ModeIndex = virtualMode ? ((uint)index << 16) | 0xFFFFu : (uint)index },
            Target = new() { Adapter = adapter, Id = (uint)(100 + index), Available = 1, ModeIndex = uint.MaxValue, Rotation = 1 },
            Flags = virtualMode ? 9u : 1u
        }).ToArray();
        DisplayMode[] modes = Enumerable.Range(0, 3).Select(index => new DisplayMode
        {
            Type = 1, Id = (uint)index, Adapter = adapter, Width = 1920, Height = 1080, PixelFormat = 4, X = index * 1920
        }).ToArray();
        return new(paths, modes, paths.Select((path, index) => new DisplayEndpoint(DisplayTopology.TargetKey(path), $"DISPLAY{index + 1}", $"monitor-{index}")).ToArray());
    }
    private sealed class Backend : IDisplayTopologyBackend
    {
        public DisplayTopology Current = Sample();
        public int Applies;
        public bool FailNextApply;
        public bool AdjustNextApply;
        public DisplayTopology Capture() => new((DisplayPath[])Current.Paths.Clone(), (DisplayMode[])Current.Modes.Clone(), (DisplayEndpoint[])Current.Endpoints.Clone());
        public void Validate(DisplayTopology topology) { }
        public void Apply(DisplayTopology topology)
        {
            Applies++;
            if (FailNextApply) { FailNextApply = false; throw new InvalidOperationException("Synthetic driver failure"); }
            Current = new((DisplayPath[])topology.Paths.Clone(), (DisplayMode[])topology.Modes.Clone(), (DisplayEndpoint[])topology.Endpoints.Clone());
            if (AdjustNextApply) { AdjustNextApply = false; Current.Modes[0].Width = 1280; }
        }
    }
    public void Dispose()
    {
        string resolved = Path.GetFullPath(_directory);
        if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test directory");
        if (File.Exists(resolved)) File.Delete(resolved);
        if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
    }
}
