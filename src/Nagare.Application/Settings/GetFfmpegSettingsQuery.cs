using Monbsoft.BrilliantMediator.Abstractions.Queries;
using Nagare.Application.Abstractions;

namespace Nagare.Application.Settings;

/// <summary>Current ffmpeg configuration, as the settings screen needs to show it (ADR-0010).</summary>
public sealed record GetFfmpegSettingsQuery : IQuery<FfmpegSettingsView>;

/// <summary>
/// The paths <b>in effect</b>, not the ones stored: when nothing was ever saved they are the
/// defaults shipped in appsettings.json, and that is what the user must see in the fields.
///
/// <para><see cref="IsUserConfigured"/> tells the two apart — same paths shown, but "this is a
/// default" and "this is your choice" are not the same message.</para>
/// </summary>
public sealed record FfmpegSettingsView(
    string ExecutablePath,
    string FfprobePath,
    string SettingsFilePath,
    bool IsUserConfigured);

public sealed class GetFfmpegSettingsHandler(IFfmpegPaths paths, IFfmpegSettingsStore store)
    : IQueryHandler<GetFfmpegSettingsQuery, FfmpegSettingsView>
{
    public async Task<FfmpegSettingsView> Handle(GetFfmpegSettingsQuery query, CancellationToken ct = default)
    {
        var stored = await store.LoadAsync(ct);
        var current = paths.Current;

        return new FfmpegSettingsView(
            current.ExecutablePath,
            current.FfprobePath,
            store.SettingsFilePath,
            IsUserConfigured: stored is not null);
    }
}
