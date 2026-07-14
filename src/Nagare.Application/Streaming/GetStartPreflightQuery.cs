using Monbsoft.BrilliantMediator.Abstractions.Queries;
using Nagare.Application.Abstractions;
using Nagare.Application.Channels;
using Nagare.Application.Profiles;
using Nagare.Domain.Profiles;
using Nagare.Domain.Sessions;

namespace Nagare.Application.Streaming;

/// <summary>
/// Why a broadcast cannot start. A STRUCTURED reason, never a sentence: the wording belongs to the
/// UI (which speaks French), the rule belongs here.
/// </summary>
public enum StartBlockReason
{
    /// <summary>Nothing blocks: the start may proceed.</summary>
    None,

    /// <summary>
    /// No verdict yet — the environment has not been probed, or the chosen file has not been
    /// analysed. Distinct from <see cref="None"/> on purpose: "not known to be startable" is not
    /// "startable". The caller must keep the start disabled while the checks are in flight.
    /// </summary>
    NotChecked,

    FfmpegMissing,
    FfprobeMissing,

    /// <summary>The selected profile encodes with NVENC, which this machine's ffmpeg does not expose.</summary>
    NvencUnavailable,

    /// <summary>A session already holds the single broadcast slot (SPEC §5).</summary>
    SessionAlreadyActive,

    ProfileNotSelected,
    ChannelNotSelected,
    InputFileNotSelected,

    /// <summary>The chosen file does not exist (any more).</summary>
    InputFileNotFound,

    /// <summary>The file is there, but ffprobe cannot decode it.</summary>
    InputFileUnreadable
}

/// <summary>Verdict of the start preflight: one reason, and the yes/no that follows from it.</summary>
public sealed record StartPreflight(StartBlockReason Reason)
{
    public bool CanStart => Reason is StartBlockReason.None;

    public static StartPreflight Ready { get; } = new(StartBlockReason.None);
    public static StartPreflight NotChecked { get; } = new(StartBlockReason.NotChecked);
}

/// <summary>
/// "May the broadcast start, and if not, why?" — the whole policy, in one place (SPEC §4, §6).
///
/// It used to live in the dashboard ViewModel, spelled out in French sentences, down to the name of
/// an ffmpeg configuration key. That put a business rule where it could be neither tested at its own
/// level nor reused, and made the UI the authority on what the domain is allowed to do.
///
/// <para><b>Why the facts are passed IN rather than fetched here.</b> The environment report costs
/// three process launches (<c>ffmpeg -version</c>, <c>ffprobe -version</c>, <c>ffmpeg -encoders</c>)
/// and the media report one more (ffprobe). This query is re-evaluated on every selection change —
/// picking a profile in a ComboBox would spawn four processes. Both reports are gathered ONCE by
/// their own queries (<see cref="Media.GetFfmpegEnvironmentQuery"/>,
/// <see cref="Media.ValidateMediaFileQuery"/>) and handed here. Caching them is plumbing, and stays
/// with the caller; DECIDING on them is the rule, and stays here. The one fact that must be read
/// live — is a session already running — is read from <see cref="ISessionMonitor"/>, because it can
/// change under the caller's feet.</para>
/// </summary>
/// <param name="Environment">Result of <see cref="Media.GetFfmpegEnvironmentQuery"/>; null = not probed yet.</param>
/// <param name="Profile">The selected encoding profile; null = none selected.</param>
/// <param name="Channel">The selected channel; null = none selected.</param>
/// <param name="InputFilePath">The chosen video file; null/blank = none chosen.</param>
/// <param name="Media">Result of <see cref="Media.ValidateMediaFileQuery"/> for <paramref name="InputFilePath"/>; null = not analysed yet.</param>
public sealed record GetStartPreflightQuery(
    FfmpegEnvironmentReport? Environment,
    StreamProfileDto? Profile,
    ChannelDto? Channel,
    string? InputFilePath,
    MediaValidationResult? Media) : IQuery<StartPreflight>;

public sealed class GetStartPreflightHandler(ISessionMonitor monitor)
    : IQueryHandler<GetStartPreflightQuery, StartPreflight>
{
    public Task<StartPreflight> Handle(GetStartPreflightQuery query, CancellationToken ct = default)
        => Task.FromResult(Evaluate(query));

    /// <summary>
    /// Order matters: it decides WHICH reason is reported when several hold, and that is the one the
    /// user is shown. Two tiers, in this order.
    ///
    /// <list type="number">
    /// <item><b>What is WRONG</b> — a broken toolchain, a file ffprobe cannot decode, a profile this
    /// machine cannot encode. Something the user must fix.</item>
    /// <item><b>What is merely NOT DONE YET</b> — no profile, no channel, no file picked.</item>
    /// </list>
    ///
    /// Tier 1 first, and not only for tone. Ranking "no channel selected" above "that file is
    /// corrupt" would SILENCE the corrupt-file message for anyone who picks their file before their
    /// channel — a perfectly ordinary way to use the page — and the error would surface only once
    /// everything else was in place.
    /// </summary>
    private StartPreflight Evaluate(GetStartPreflightQuery query)
    {
        if (query.Environment is not { } environment)
            return StartPreflight.NotChecked;

        // ---- tier 1: what is wrong ----

        if (!environment.FfmpegAvailable)
            return new StartPreflight(StartBlockReason.FfmpegMissing);

        // ffprobe is not optional: without it no file can be validated, so no start could ever be
        // cleared — better to say so than to let the user pick a file and wonder.
        if (!environment.FfprobeAvailable)
            return new StartPreflight(StartBlockReason.FfprobeMissing);

        // Read live: the coordinator refuses a second session anyway (it owns the truth). Reporting
        // it here is what lets the UI disable the button instead of offering a click that throws.
        if (monitor.Current is { } session && session.Status.IsActive())
            return new StartPreflight(StartBlockReason.SessionAlreadyActive);

        // Readable=false and a non-null Error are the two ways ffprobe reports a file it cannot
        // decode; either one is disqualifying.
        if (query.Media is { } media)
        {
            if (!media.Exists)
                return new StartPreflight(StartBlockReason.InputFileNotFound);

            if (!media.Readable || media.Error is not null)
                return new StartPreflight(StartBlockReason.InputFileUnreadable);
        }

        if (query.Profile is { } selected && selected.Video.Codec.RequiresNvenc() && !environment.NvencAvailable)
            return new StartPreflight(StartBlockReason.NvencUnavailable);

        // ---- tier 2: what the user has yet to do ----

        if (query.Profile is null)
            return new StartPreflight(StartBlockReason.ProfileNotSelected);

        if (query.Channel is null)
            return new StartPreflight(StartBlockReason.ChannelNotSelected);

        if (string.IsNullOrWhiteSpace(query.InputFilePath))
            return new StartPreflight(StartBlockReason.InputFileNotSelected);

        // A file is chosen but ffprobe has not answered yet: no verdict, and therefore no start.
        if (query.Media is null)
            return StartPreflight.NotChecked;

        return StartPreflight.Ready;
    }
}
