using Nagare.Domain.Common;

namespace Nagare.Domain.Sessions;

/// <summary>
/// A live broadcast in progress. Aggregate root carrying an explicit state
/// machine (ARCHITECTURE.md §2.4). Not persisted in iteration 1 — lives in memory.
///
/// Allowed transitions only; any other transition throws <see cref="DomainException"/>.
/// Design rule: an initial failure (state <see cref="SessionStatus.Starting"/>) goes
/// straight to <see cref="SessionStatus.Failed"/> without backoff; automatic
/// reconnection with backoff is reserved for drops of an already-established stream
/// (state <see cref="SessionStatus.Running"/>).
/// </summary>
public sealed class StreamSession : AggregateRoot
{
    /// <summary>
    /// Typo guard on the requested duration, not a product limit (ADR-0009, invariant S2). EXPOSED
    /// so the UI can bound its input with the very value the invariant enforces — the displayed
    /// bound and the applied bound cannot drift apart, exactly as the encoding combo boxes read
    /// their allowed values from the domain.
    /// </summary>
    public static readonly TimeSpan MaxAllowedDuration = TimeSpan.FromHours(24);

    public SessionId Id { get; }
    public ProfileId ProfileId { get; }
    public ChannelId ChannelId { get; }
    public string InputFilePath { get; }
    public SessionStatus Status { get; private set; }
    public int ReconnectAttempts { get; private set; }
    public ReconnectPolicy Policy { get; }

    /// <summary>
    /// How long the user agreed to broadcast; null = no limit (ADR-0009). The INTENTION lives
    /// here; the resulting instant does not. The aggregate has no clock to decide against —
    /// <c>Nagare.Domain</c> has no <c>TimeProvider</c> and will not get one — so the coordinator
    /// owns the deadline and applies it.
    /// </summary>
    public TimeSpan? MaxDuration { get; }

    /// <summary>Null until the session is stopped — and null forever on a session that FAILED.</summary>
    public SessionStopReason? StopReason { get; private set; }

    /// <summary>Always passed through scrubbing before assignment (ARCHITECTURE.md §6.3).</summary>
    public string? LastError { get; private set; }

    private StreamSession(
        SessionId id,
        ProfileId profileId,
        ChannelId channelId,
        string inputFilePath,
        ReconnectPolicy policy,
        TimeSpan? maxDuration)
    {
        Id = id;
        ProfileId = profileId;
        ChannelId = channelId;
        InputFilePath = inputFilePath;
        Policy = policy;
        MaxDuration = maxDuration;
        Status = SessionStatus.Starting;
        ReconnectAttempts = 0;
    }

    /// <summary>
    /// Creates a session in <see cref="SessionStatus.Starting"/> and emits <see cref="SessionLaunched"/>.
    /// </summary>
    /// <param name="maxDuration">Maximum broadcast duration, null (the default) = no limit. This
    /// default is a BUSINESS rule — "no duration given means broadcast until stopped" — which is
    /// why it lives here and nowhere else on the way in (ADR-0009 §4).</param>
    public static StreamSession Launch(
        ProfileId profileId,
        ChannelId channelId,
        string inputFilePath,
        ReconnectPolicy policy,
        TimeSpan? maxDuration = null)
    {
        if (string.IsNullOrWhiteSpace(inputFilePath))
            throw new DomainException("The input file path is required.");
        if (policy is null)
            throw new DomainException("A reconnection policy is required.");

        if (maxDuration is { } duration)
        {
            // S1 / S2 (ADR-0009). A window of zero or less means nothing, and a duration beyond the
            // guard is a typo — 240 hours instead of 24 would hold the broadcast slot for ten days.
            if (duration <= TimeSpan.Zero)
                throw new DomainException("The maximum duration must be greater than zero.");
            if (duration > MaxAllowedDuration)
                throw new DomainException(
                    $"The maximum duration cannot exceed {MaxAllowedDuration.TotalHours:0} hours.");
        }

        var session = new StreamSession(SessionId.New(), profileId, channelId, inputFilePath, policy, maxDuration);
        session.RaiseDomainEvent(new SessionLaunched(session.Id, profileId, channelId, DateTimeOffset.UtcNow));
        return session;
    }

    /// <summary>
    /// Starting -> Running (emits <see cref="SessionStarted"/>) or
    /// Reconnecting -> Running (emits <see cref="SessionRecovered"/>, resets the attempt counter).
    /// </summary>
    public void MarkRunning()
    {
        switch (Status)
        {
            case SessionStatus.Starting:
                Status = SessionStatus.Running;
                RaiseDomainEvent(new SessionStarted(Id, DateTimeOffset.UtcNow));
                break;

            case SessionStatus.Reconnecting:
                var afterAttempts = ReconnectAttempts;
                ReconnectAttempts = 0;
                Status = SessionStatus.Running;
                RaiseDomainEvent(new SessionRecovered(Id, afterAttempts, DateTimeOffset.UtcNow));
                break;

            default:
                throw InvalidTransition(nameof(MarkRunning));
        }
    }

    /// <summary>
    /// Running|Reconnecting -> Reconnecting. From Running a drop was detected; from
    /// Reconnecting a relaunch died before producing stats, so a further attempt is counted.
    /// Increments the attempt counter; once the attempts are exhausted, goes to Failed
    /// (emits <see cref="SessionFailed"/>) instead of Reconnecting (emits <see cref="ReconnectStarted"/>).
    /// A successful recovery (<see cref="MarkRunning"/>) restores the whole attempt budget.
    /// </summary>
    public void BeginReconnect(string reason)
    {
        if (Status is not (SessionStatus.Running or SessionStatus.Reconnecting))
            throw InvalidTransition(nameof(BeginReconnect));

        var attempt = ReconnectAttempts + 1;

        if (attempt > Policy.MaxAttempts)
        {
            LastError = reason;
            Status = SessionStatus.Failed;
            RaiseDomainEvent(new SessionFailed(Id, reason, DateTimeOffset.UtcNow));
            return;
        }

        ReconnectAttempts = attempt;
        Status = SessionStatus.Reconnecting;
        RaiseDomainEvent(new ReconnectStarted(Id, attempt, Policy.DelayFor(attempt), reason, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Starting|Running|Reconnecting -> Stopped (emits <see cref="SessionStopped"/>).
    ///
    /// The reason has NO default value on purpose (ADR-0009): the compiler then forces every call
    /// site to say why it stops. A default would let a future automatic stop label itself "manual"
    /// by omission, and the events are the audit trail of the broadcast.
    ///
    /// Reaching this from <see cref="SessionStatus.Reconnecting"/> is deliberate and is what makes
    /// "the scheduled stop wins over the backoff" work without a new transition: the user asked for
    /// N hours, retrying past them contradicts that.
    /// </summary>
    public void Stop(SessionStopReason reason)
    {
        if (Status is not (SessionStatus.Starting or SessionStatus.Running or SessionStatus.Reconnecting))
            throw InvalidTransition(nameof(Stop));

        // S3 (ADR-0009). Without a limit there is no duration to elapse: a trigger reaching such a
        // session is a coordinator bug, and it must fail LOUDLY rather than mislabel the stop. This
        // is also what keeps MaxDuration from being dead data.
        if (reason is SessionStopReason.DurationElapsed && MaxDuration is null)
            throw new DomainException(
                "A session without a maximum duration cannot be stopped because its duration elapsed.");

        StopReason = reason;
        Status = SessionStatus.Stopped;
        RaiseDomainEvent(new SessionStopped(Id, reason, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Starting|Running|Reconnecting -> Failed (emits <see cref="SessionFailed"/>).
    /// An EXPLICIT giving-up: the launch failed, the attempts are exhausted, or an internal fault
    /// leaves nothing able to drive the session any further. It is deliberately allowed from
    /// Running: a live session that can no longer be driven must be able to say so truthfully.
    ///
    /// This does NOT open a shortcut for ffmpeg exits — a drop of an established stream still goes
    /// through <see cref="BeginReconnect"/> and consumes the reconnection budget.
    ///
    /// <see cref="StopReason"/> stays null: a failure is not a stop, and
    /// <see cref="SessionFailed"/> already says what happened.
    /// </summary>
    public void MarkFailed(string reason)
    {
        if (Status is not (SessionStatus.Starting or SessionStatus.Running or SessionStatus.Reconnecting))
            throw InvalidTransition(nameof(MarkFailed));

        LastError = reason;
        Status = SessionStatus.Failed;
        RaiseDomainEvent(new SessionFailed(Id, reason, DateTimeOffset.UtcNow));
    }

    private DomainException InvalidTransition(string operation)
        => new($"Invalid transition: {operation} is not allowed from status {Status}.");
}
