using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;

namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// Media validation via ffprobe (ARCHITECTURE.md §4.2, §6.2). Runs
/// `ffprobe -v quiet -print_format json -show_format -show_streams &lt;file&gt;` and maps the
/// JSON to <see cref="MediaValidationResult"/>. Never executed by the unit tests.
/// </summary>
public sealed class FfprobeService(IOptions<FfmpegOptions> options) : IFfprobeService
{
    private readonly FfmpegOptions _options = options.Value;

    public async Task<MediaValidationResult> AnalyzeAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            return new MediaValidationResult(false, false, null, null, null, null, null, null, "File not found.");

        try
        {
            var json = await RunFfprobeAsync(filePath, ct);
            return Map(json);
        }
        catch (Exception ex)
        {
            return new MediaValidationResult(true, false, null, null, null, null, null, null, ex.Message);
        }
    }

    private async Task<string> RunFfprobeAsync(string filePath, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ResolvedFfprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in new[]
                 {
                     "-v", "quiet",
                     "-print_format", "json",
                     "-show_format",
                     "-show_streams",
                     filePath
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start ffprobe.");

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return output;
    }

    private static MediaValidationResult Map(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        TimeSpan? duration = null;
        if (root.TryGetProperty("format", out var format)
            && format.TryGetProperty("duration", out var durationElement)
            && double.TryParse(durationElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            duration = TimeSpan.FromSeconds(seconds);
        }

        int? width = null, height = null;
        double? fps = null;
        string? videoCodec = null, audioCodec = null;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var t) ? t.GetString() : null;
                var codecName = stream.TryGetProperty("codec_name", out var n) ? n.GetString() : null;

                if (codecType == "video" && videoCodec is null)
                {
                    videoCodec = codecName;
                    if (stream.TryGetProperty("width", out var w)) width = w.GetInt32();
                    if (stream.TryGetProperty("height", out var h)) height = h.GetInt32();
                    if (stream.TryGetProperty("r_frame_rate", out var r)) fps = ParseFrameRate(r.GetString());
                }
                else if (codecType == "audio" && audioCodec is null)
                {
                    audioCodec = codecName;
                }
            }
        }

        return new MediaValidationResult(true, true, duration, width, height, fps, videoCodec, audioCodec, null);
    }

    private static double? ParseFrameRate(string? rFrameRate)
    {
        if (string.IsNullOrWhiteSpace(rFrameRate))
            return null;

        var parts = rFrameRate.Split('/');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den)
            && den != 0)
        {
            return num / den;
        }

        return double.TryParse(rFrameRate, NumberStyles.Float, CultureInfo.InvariantCulture, out var single) ? single : null;
    }
}
