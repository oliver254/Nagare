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
    /// version, and NVENC availability via `ffmpeg -encoders`.
    /// </summary>
    Task<FfmpegEnvironmentReport> CheckAsync(CancellationToken ct);
}
