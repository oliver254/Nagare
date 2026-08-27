using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;

namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// The singleton that answers "which ffmpeg?" (ADR-0010, §4). Starts on the default shipped in
/// appsettings.json (<see cref="FfmpegOptions"/>) and is overwritten — at startup by
/// <see cref="FfmpegSettingsInitializer"/>, later by a save — with the user's choice.
///
/// <para>Thread-safety is one <c>volatile</c> field and nothing else, because the value is an
/// immutable record: a reader either sees the old pair or the new one, never a half-applied one.
/// A lock would buy nothing here, and readers are on the hot path of every ffmpeg launch.</para>
/// </summary>
public sealed class FfmpegPathProvider : IFfmpegPaths
{
    private volatile FfmpegPathSettings _current;

    public FfmpegPathProvider(IOptions<FfmpegOptions> options)
    {
        var defaults = options.Value;
        _current = new FfmpegPathSettings(defaults.ExecutablePath, defaults.FfprobePath);
    }

    public FfmpegPathSettings Current => _current;

    public void Apply(FfmpegPathSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _current = settings;
    }
}
