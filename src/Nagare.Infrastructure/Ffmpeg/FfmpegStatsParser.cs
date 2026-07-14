using System.Globalization;
using System.Text.RegularExpressions;
using Nagare.Application.Abstractions;

namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// Pure parser of ffmpeg progression lines (ARCHITECTURE.md §6.2). Extracts
/// frame/fps/bitrate/speed/drop/dup/time; returns null for non-stats lines.
/// Internal but unit-tested (InternalsVisibleTo).
/// </summary>
internal static partial class FfmpegStatsParser
{
    public static FfmpegStats? TryParse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        // A progression line always carries "frame=" and "time=".
        var frameMatch = FrameRegex().Match(line);
        var timeMatch = TimeRegex().Match(line);
        if (!frameMatch.Success || !timeMatch.Success)
            return null;

        var frame = long.Parse(frameMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        var fps = ParseDouble(FpsRegex().Match(line));
        var bitrate = ParseBitrate(BitrateRegex().Match(line));
        var speed = ParseDouble(SpeedRegex().Match(line));
        var drop = ParseInt(DropRegex().Match(line));
        var dup = ParseInt(DupRegex().Match(line));
        var time = ParseTime(timeMatch);

        return new FfmpegStats(frame, fps, bitrate, speed, drop, dup, time);
    }

    private static double ParseDouble(Match match)
        => match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0d;

    private static int ParseInt(Match match)
        => match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static double ParseBitrate(Match match)
        => match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0d;

    private static TimeSpan ParseTime(Match match)
    {
        var hours = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var seconds = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        return new TimeSpan(0, hours, minutes, 0) + TimeSpan.FromSeconds(seconds);
    }

    [GeneratedRegex(@"frame=\s*(\d+)", RegexOptions.Compiled)]
    private static partial Regex FrameRegex();

    [GeneratedRegex(@"fps=\s*([\d.]+)", RegexOptions.Compiled)]
    private static partial Regex FpsRegex();

    [GeneratedRegex(@"bitrate=\s*([\d.]+)\s*kbits/s", RegexOptions.Compiled)]
    private static partial Regex BitrateRegex();

    [GeneratedRegex(@"speed=\s*([\d.]+)x", RegexOptions.Compiled)]
    private static partial Regex SpeedRegex();

    [GeneratedRegex(@"drop=\s*(\d+)", RegexOptions.Compiled)]
    private static partial Regex DropRegex();

    [GeneratedRegex(@"dup=\s*(\d+)", RegexOptions.Compiled)]
    private static partial Regex DupRegex();

    [GeneratedRegex(@"time=\s*(\d+):(\d{2}):(\d{2}(?:\.\d+)?)", RegexOptions.Compiled)]
    private static partial Regex TimeRegex();
}
