using Nagare.Domain.Channels;
using Nagare.Domain.Profiles;

namespace Nagare.Application.Abstractions;

/// <summary>
/// Result of the command builder. ToString() returns MaskedCommandLine (never the
/// real arguments). Arguments and Secrets must NEVER be logged nor serialized: they
/// travel opaquely from the builder (Infra) to the runner (Infra) through the
/// handler (App). (ARCHITECTURE.md §4.2)
/// </summary>
public sealed record FfmpegCommand(
    IReadOnlyList<string> Arguments,      // real arguments, plaintext key included
    string MaskedCommandLine,             // displayable version, key replaced by ****
    IReadOnlyList<string> Secrets)        // values to scrub from any process output
{
    public override string ToString() => MaskedCommandLine;
}

public interface IFfmpegCommandBuilder
{
    /// <summary>
    /// Maps profile + channel + file -> ffmpeg arguments, STRICT canonical order
    /// (ARCHITECTURE.md §6.1). Decrypts the key internally (IStreamKeyProtector) — the
    /// plaintext only leaks into Arguments/Secrets, opaque by convention.
    /// </summary>
    FfmpegCommand Build(StreamProfile profile, Channel channel, string inputFilePath);
}

public interface IFfmpegProcessRunner : IAsyncDisposable
{
    Task StartAsync(FfmpegCommand command, CancellationToken ct);

    /// <summary>Clean stop: 'q' on stdin, wait gracePeriod, otherwise Kill(entireProcessTree: true).</summary>
    Task StopAsync(TimeSpan gracePeriod, CancellationToken ct);

    bool IsRunning { get; }

    event Action<string> OutputLineReceived;   // stderr/stdout lines ALREADY scrubbed (§6.3)
    event Action<FfmpegStats> StatsReceived;    // parsed progression lines
    event Action<int> Exited;                   // exit code
}

public interface IFfprobeService
{
    Task<MediaValidationResult> AnalyzeAsync(string filePath, CancellationToken ct);
}

public interface IFfmpegEnvironmentProbe
{
    /// <summary>
    /// Startup check: ffmpeg/ffprobe present (configured path, otherwise PATH),
    /// version, and NVENC availability via `ffmpeg -encoders`. Probes the paths
    /// currently in effect (<see cref="IFfmpegPaths.Current"/>).
    /// </summary>
    Task<FfmpegEnvironmentReport> CheckAsync(CancellationToken ct);

    /// <summary>
    /// Same check, on the paths GIVEN rather than on the ones in effect. This is what makes
    /// a "Test" button possible BEFORE saving: the user gets a verdict on a candidate pair
    /// without the application already living with it (ADR-0010).
    /// </summary>
    Task<FfmpegEnvironmentReport> CheckAsync(FfmpegPathSettings paths, CancellationToken ct);
}

/// <summary>
/// A requested ffmpeg/ffprobe path pair. An empty string is a legitimate value: it means
/// "resolve the binary from the PATH" (ADR-0010).
/// </summary>
public sealed record FfmpegPathSettings(string ExecutablePath, string FfprobePath)
{
    /// <summary>Nothing configured on either side — both binaries come from the PATH.</summary>
    public static FfmpegPathSettings Empty { get; } = new(string.Empty, string.Empty);

    public string ResolvedFfmpeg => string.IsNullOrWhiteSpace(ExecutablePath) ? "ffmpeg" : ExecutablePath;

    public string ResolvedFfprobe => string.IsNullOrWhiteSpace(FfprobePath) ? "ffprobe" : FfprobePath;
}

/// <summary>
/// Runtime truth of the paths in effect (ADR-0010, §4). A singleton, mutated
/// <b>synchronously</b> when the user saves: every consumer — probe, preflight, ffmpeg launch —
/// sees the new paths as soon as the save call returns, with no reload window in between.
///
/// <para>Reading and writing are one interface on purpose only because they are one fact; the
/// three adapters that launch a binary depend on it to <i>read</i>, and none of them ever calls
/// <see cref="Apply"/>.</para>
/// </summary>
public interface IFfmpegPaths
{
    FfmpegPathSettings Current { get; }

    void Apply(FfmpegPathSettings settings);
}

/// <summary>
/// Persistence of the user configuration (<c>%APPDATA%\Nagare\settings.json</c>, ADR-0010).
/// Plaintext file: it carries paths and nothing else — stream keys stay under ADR-0005.
/// </summary>
public interface IFfmpegSettingsStore
{
    /// <summary>Absolute path of the settings file, shown to the user so it can be found.</summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Returns null when the user never configured anything — no file, an empty file, or a file
    /// this application cannot make sense of. Never throws on a corrupted file: the application
    /// must still open, since opening it is how the user fixes the configuration.
    /// </summary>
    Task<FfmpegPathSettings?> LoadAsync(CancellationToken ct);

    Task SaveAsync(FfmpegPathSettings settings, CancellationToken ct);
}

/// <summary>Automatic detection of the ffmpeg/ffprobe binaries (ADR-0010).</summary>
public interface IFfmpegLocator
{
    /// <summary>
    /// Probes the well-known install locations. Returns <see cref="FfmpegPathSettings.Empty"/>
    /// when nothing is found, and a half-filled pair when only one of the two binaries is.
    /// </summary>
    Task<FfmpegPathSettings> LocateAsync(CancellationToken ct);
}
