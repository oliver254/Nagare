using Nagare.Domain.Common;

namespace Nagare.Application.Abstractions;

/// <summary>
/// Runtime coordinator holding the single active <see cref="Domain.Sessions.StreamSession"/>
/// (ARCHITECTURE.md §5). The StartStream/StopStream handlers delegate to it. The
/// "single active session at a time" rule is an application invariant, not a domain one.
/// </summary>
public interface IStreamSessionCoordinator
{
    bool HasActiveSession { get; }

    /// <summary>
    /// Loads profile + channel, builds the ffmpeg command, starts the runner and
    /// tracks the session. Throws if a session is already active.
    /// </summary>
    /// <param name="maxDuration">Maximum broadcast duration, null = no limit. When set, the
    /// coordinator arms the deadline and stops the session on its own once it is reached
    /// (ADR-0009). No default value here: at the application boundary every caller states what it
    /// wants, so a duration entered by the user can never be silently dropped.</param>
    Task<SessionId> StartAsync(
        ProfileId profileId,
        ChannelId channelId,
        string inputFilePath,
        TimeSpan? maxDuration,
        CancellationToken ct);

    /// <summary>Cleanly stops the active session (no-op if none).</summary>
    Task StopAsync(CancellationToken ct);
}
