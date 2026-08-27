namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// ffmpeg/ffprobe binary paths SHIPPED as defaults, bound from the configuration section
/// Nagare:Ffmpeg (ARCHITECTURE.md §6.2). Empty path -> resolve from PATH.
///
/// <para>Since ADR-0010 this is no longer the runtime truth: it is read once, by
/// <see cref="FfmpegPathProvider"/>, as the value in effect until the user configures one of
/// their own. Every consumer that launches a binary goes through
/// <see cref="Nagare.Application.Abstractions.IFfmpegPaths"/> — which is why the Resolved*
/// members moved there and no longer exist here.</para>
/// </summary>
public sealed class FfmpegOptions
{
    public const string SectionName = "Nagare:Ffmpeg";

    /// <summary>Default ffmpeg path; falls back to "ffmpeg" on PATH when empty.</summary>
    public string ExecutablePath { get; set; } = "ffmpeg";

    /// <summary>Default ffprobe path; falls back to "ffprobe" on PATH when empty.</summary>
    public string FfprobePath { get; set; } = "ffprobe";
}
