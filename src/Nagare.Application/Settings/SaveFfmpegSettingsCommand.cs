using Monbsoft.BrilliantMediator.Abstractions.Commands;
using Nagare.Application.Abstractions;

namespace Nagare.Application.Settings;

/// <summary>
/// Saves the ffmpeg/ffprobe paths chosen by the user (ADR-0010). An empty field is legitimate:
/// it means "resolve from the PATH", and is accepted as long as the binary answers from there.
/// </summary>
public sealed record SaveFfmpegSettingsCommand(string ExecutablePath, string FfprobePath)
    : ICommand<SaveFfmpegSettingsResult>;

/// <summary>
/// <see cref="Saved"/> false means NOTHING was written and nothing changed — <see cref="Report"/>
/// carries why. It is returned in both cases so the screen can show the verdict of the probe it
/// just ran, success included.
/// </summary>
public sealed record SaveFfmpegSettingsResult(bool Saved, FfmpegEnvironmentReport Report);

/// <summary>
/// Probe first, write second, publish third — and each step is a gate for the next.
///
/// <para>The order is the whole point. Writing a pair that does not run would persist a broken
/// configuration across restarts, and the application would come back up just as stuck as it went
/// down; applying before the write succeeded would leave memory and disk disagreeing until the
/// next restart silently undid the user's change.</para>
/// </summary>
public sealed class SaveFfmpegSettingsHandler(
    IFfmpegEnvironmentProbe probe,
    IFfmpegSettingsStore store,
    IFfmpegPaths paths)
    : ICommandHandler<SaveFfmpegSettingsCommand, SaveFfmpegSettingsResult>
{
    public async Task<SaveFfmpegSettingsResult> Handle(
        SaveFfmpegSettingsCommand command,
        CancellationToken ct = default)
    {
        var settings = FfmpegPathInput.Normalize(command.ExecutablePath, command.FfprobePath);

        var report = await probe.CheckAsync(settings, ct);
        if (!report.FfmpegAvailable || !report.FfprobeAvailable)
            return new SaveFfmpegSettingsResult(Saved: false, report);

        await store.SaveAsync(settings, ct);

        // Only once the file is on disk. Synchronous, so the very next query — a "Test" clicked
        // right after "Save" — already reads these paths (ADR-0010).
        paths.Apply(settings);

        return new SaveFfmpegSettingsResult(Saved: true, report);
    }
}
