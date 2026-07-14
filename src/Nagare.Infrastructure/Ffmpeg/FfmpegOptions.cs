namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// ffmpeg/ffprobe binary paths (ARCHITECTURE.md §6.2). Empty path -> resolve from PATH.
/// Bound from configuration section Nagare:Ffmpeg.
/// </summary>
public sealed class FfmpegOptions
{
    public const string SectionName = "Nagare:Ffmpeg";

    /// <summary>Configured ffmpeg path; falls back to "ffmpeg" on PATH when empty.</summary>
    public string ExecutablePath { get; set; } = "ffmpeg";

    /// <summary>Configured ffprobe path; falls back to "ffprobe" on PATH when empty.</summary>
    public string FfprobePath { get; set; } = "ffprobe";

    public string ResolvedFfmpeg => string.IsNullOrWhiteSpace(ExecutablePath) ? "ffmpeg" : ExecutablePath;
    public string ResolvedFfprobe => string.IsNullOrWhiteSpace(FfprobePath) ? "ffprobe" : FfprobePath;
}
