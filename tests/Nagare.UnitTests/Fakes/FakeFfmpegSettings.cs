using Nagare.Application.Abstractions;

namespace Nagare.UnitTests.Fakes;

/// <summary>In-memory <see cref="IFfmpegPaths"/>. Counts applications: the save rule is as much about
/// NOT applying as about applying.</summary>
public sealed class FakeFfmpegPaths(FfmpegPathSettings? initial = null) : IFfmpegPaths
{
    public FfmpegPathSettings Current { get; private set; } = initial ?? FfmpegPathSettings.Empty;

    public int ApplyCallCount { get; private set; }

    public void Apply(FfmpegPathSettings settings)
    {
        ApplyCallCount++;
        Current = settings;
    }
}

/// <summary>
/// In-memory <see cref="IFfmpegSettingsStore"/>. <see cref="SaveCallCount"/> is what proves
/// "nothing was written", which no assertion on the returned result can.
/// </summary>
public sealed class FakeFfmpegSettingsStore : IFfmpegSettingsStore
{
    public string SettingsFilePath { get; set; } = @"C:\fake\Nagare\settings.json";

    public FfmpegPathSettings? Stored { get; set; }

    public int SaveCallCount { get; private set; }

    /// <summary>Simulates an unreadable file — the startup path that must not take the host down.</summary>
    public Exception? LoadFailure { get; set; }

    /// <summary>Simulates a write failure — a read-only folder, a full disk.</summary>
    public Exception? SaveFailure { get; set; }

    public Task<FfmpegPathSettings?> LoadAsync(CancellationToken ct)
        => LoadFailure is null ? Task.FromResult(Stored) : Task.FromException<FfmpegPathSettings?>(LoadFailure);

    public Task SaveAsync(FfmpegPathSettings settings, CancellationToken ct)
    {
        if (SaveFailure is not null)
            return Task.FromException(SaveFailure);

        SaveCallCount++;
        Stored = settings;
        return Task.CompletedTask;
    }
}

/// <summary>
/// <see cref="IFfmpegEnvironmentProbe"/> that answers from a declared list of runnable binaries.
/// A bare "ffmpeg"/"ffprobe" in that list stands for "the PATH resolves it" — which is how an
/// empty field is tested without depending on the machine running the suite.
/// </summary>
public sealed class FakeFfmpegEnvironmentProbe(params string[] runnableBinaries) : IFfmpegEnvironmentProbe
{
    private readonly HashSet<string> _runnable = new(runnableBinaries, StringComparer.OrdinalIgnoreCase);

    /// <summary>What the parameterless overload probes, standing in for IFfmpegPaths.Current.</summary>
    public FfmpegPathSettings CurrentPaths { get; set; } = FfmpegPathSettings.Empty;

    /// <summary>The pair actually probed — the assertion for "tested what was asked, not what is in effect".</summary>
    public FfmpegPathSettings? LastProbed { get; private set; }

    public Task<FfmpegEnvironmentReport> CheckAsync(CancellationToken ct) => CheckAsync(CurrentPaths, ct);

    public Task<FfmpegEnvironmentReport> CheckAsync(FfmpegPathSettings paths, CancellationToken ct)
    {
        LastProbed = paths;

        var ffmpeg = _runnable.Contains(paths.ResolvedFfmpeg);
        var ffprobe = _runnable.Contains(paths.ResolvedFfprobe);

        return Task.FromResult(new FfmpegEnvironmentReport(
            ffmpeg,
            ffprobe,
            ffmpeg ? "ffmpeg version 7.1" : null,
            NvencAvailable: ffmpeg,
            Error: ffmpeg && ffprobe ? null : "not found"));
    }
}

/// <summary><see cref="IFfmpegLocator"/> returning a canned detection result.</summary>
public sealed class FakeFfmpegLocator(FfmpegPathSettings? found = null) : IFfmpegLocator
{
    public Task<FfmpegPathSettings> LocateAsync(CancellationToken ct)
        => Task.FromResult(found ?? FfmpegPathSettings.Empty);
}
