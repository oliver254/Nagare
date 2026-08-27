using Nagare.Application.Abstractions;

namespace Nagare.Application.Settings;

/// <summary>
/// Turns what a text box produces into a <see cref="FfmpegPathSettings"/>. Trimming is not
/// cosmetic: a path pasted from Explorer often carries a trailing space, and the value tested
/// must be the very value saved — otherwise "Test" and "Save" could disagree.
/// </summary>
internal static class FfmpegPathInput
{
    public static FfmpegPathSettings Normalize(string? executablePath, string? ffprobePath)
        => new(executablePath?.Trim() ?? string.Empty, ffprobePath?.Trim() ?? string.Empty);
}
