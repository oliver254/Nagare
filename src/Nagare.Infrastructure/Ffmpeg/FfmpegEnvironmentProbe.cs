using System.Diagnostics;
using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;

namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// Startup environment check (ARCHITECTURE.md §4.2). Verifies ffmpeg/ffprobe presence and
/// NVENC availability via `ffmpeg -encoders`. Gracefully reports absence (binaries are not
/// on the dev machine — addendum SPEC).
/// </summary>
public sealed class FfmpegEnvironmentProbe(IOptions<FfmpegOptions> options) : IFfmpegEnvironmentProbe
{
    private readonly FfmpegOptions _options = options.Value;

    public async Task<FfmpegEnvironmentReport> CheckAsync(CancellationToken ct)
    {
        try
        {
            var version = await TryRunAsync(_options.ResolvedFfmpeg, ["-version"], ct);
            var ffmpegAvailable = version is not null;
            var ffprobeAvailable = await TryRunAsync(_options.ResolvedFfprobe, ["-version"], ct) is not null;

            var nvenc = false;
            if (ffmpegAvailable)
            {
                var encoders = await TryRunAsync(_options.ResolvedFfmpeg, ["-hide_banner", "-encoders"], ct);
                nvenc = encoders?.Contains("h264_nvenc", StringComparison.OrdinalIgnoreCase) == true;
            }

            var versionLine = version?.Split('\n', 2)[0].Trim();

            return new FfmpegEnvironmentReport(
                ffmpegAvailable,
                ffprobeAvailable,
                versionLine,
                nvenc,
                ffmpegAvailable ? null : "ffmpeg not found (configured path or PATH).");
        }
        catch (Exception ex)
        {
            return new FfmpegEnvironmentReport(false, false, null, false, ex.Message);
        }
    }

    private static async Task<string?> TryRunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return output;
        }
        catch
        {
            // Binary missing / not launchable -> treated as unavailable.
            return null;
        }
    }
}
