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
    public SessionId Id { get; }
    public ProfileId ProfileId { get; }
    public ChannelId ChannelId { get; }
    public string InputFilePath { get; }
    public SessionStatus Status { get; private set; }
    public int ReconnectAttempts { get; private set; }
    public ReconnectPolicy Policy { get; }

    /// <summary>Always passed through scrubbing before assignment (ARCHITECTURE.md §6.3).</summary>
    public string? LastError { get; private set; }

    private StreamSession(
        SessionId id,
        ProfileId profileId,
        ChannelId channelId,
        string inputFilePath,
        ReconnectPolicy policy)
    {
        Id = id;
        ProfileId = profileId;
        ChannelId = channelId;
        InputFilePath = inputFilePath;
        Policy = policy;
        Status = SessionStatus.Starting;
        ReconnectAttempts = 0;
    }

    /// <summary>Creates a session in <see cref="SessionStatus.Starting"/> and emits <see cref="SessionLaunched"/>.</summary>
    public static StreamSession Launch(ProfileId profileId, ChannelId channelId, string inputFilePath, ReconnectPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(inputFilePath))
            throw new DomainException("The input file path is required.");
        if (policy is null)
            throw new DomainException("A reconnection policy is required.");

        var session = new StreamSession(SessionId.New(), profileId, channelId, inputFilePath, policy);
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

    /// <summary>Starting|Running|Reconnecting -> Stopped (emits <see cref="SessionStopped"/>).</summary>
    public void Stop()
    {
        if (Status is not (SessionStatus.Starting or SessionStatus.Running or SessionStatus.Reconnecting))
            throw InvalidTransition(nameof(Stop));

        Status = SessionStatus.Stopped;
        RaiseDomainEvent(new SessionStopped(Id, DateTimeOffset.UtcNow));
    }

    /// <summary>Starting|Reconnecting -> Failed (emits <see cref="SessionFailed"/>).</summary>
    public void MarkFailed(string reason)
    {
        if (Status is not (SessionStatus.Starting or SessionStatus.Reconnecting))
            throw InvalidTransition(nameof(MarkFailed));

        LastError = reason;
        Status = SessionStatus.Failed;
        RaiseDomainEvent(new SessionFailed(Id, reason, DateTimeOffset.UtcNow));
    }

    private DomainException InvalidTransition(string operation)
        => new($"Invalid transition: {operation} is not allowed from status {Status}.");
}
