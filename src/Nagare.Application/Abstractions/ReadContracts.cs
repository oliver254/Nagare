using Nagare.Domain.Common;
using Nagare.Domain.Sessions;

namespace Nagare.Application.Abstractions;

/// <summary>Parsed ffmpeg progression line (ARCHITECTURE.md §3.3).</summary>
public sealed record FfmpegStats(
    long Frame,
    double Fps,
    double BitrateKbps,
    double Speed,
    int DroppedFrames,
    int DupFrames,
    TimeSpan Time);

/// <summary>Health indicator: Warning if Speed &lt; 1.0 or growing drops.</summary>
public enum HealthIndicator { Ok, Warning }

/// <summary>Read-only projection of the active session for the UI (ARCHITECTURE.md §3.3).</summary>
public sealed record SessionSnapshot(
    SessionId Id,
    SessionStatus Status,
    FfmpegStats? Stats,
    HealthIndicator Health,
    int ReconnectAttempts,
    string? LastError);

/// <summary>Result of ffprobe media analysis (ARCHITECTURE.md §3.2, ValidateMediaFileQuery).</summary>
public sealed record MediaValidationResult(
    bool Exists,
    bool Readable,
    TimeSpan? Duration,
    int? Width,
    int? Height,
    double? Fps,
    string? VideoCodec,
    string? AudioCodec,
    string? Error);

/// <summary>Startup ffmpeg environment report (ARCHITECTURE.md §4.2).</summary>
public sealed record FfmpegEnvironmentReport(
    bool FfmpegAvailable,
    bool FfprobeAvailable,
    string? FfmpegVersion,
    bool NvencAvailable,
    string? Error);
