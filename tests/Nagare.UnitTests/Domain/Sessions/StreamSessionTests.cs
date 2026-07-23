using Nagare.Domain.Common;
using Nagare.Domain.Sessions;

namespace Nagare.UnitTests.Domain.Sessions;

/// <summary>
/// State machine of the session aggregate (ARCHITECTURE.md §2.4, domain-model.md).
/// Every allowed transition emits its domain event; every other one throws
/// <see cref="DomainException"/>. Design rule: a failure while <see cref="SessionStatus.Starting"/>
/// goes straight to Failed without backoff — the backoff is reserved for drops of an
/// already-established stream (<see cref="SessionStatus.Running"/>).
/// </summary>
public sealed class StreamSessionTests
{
    private static readonly ReconnectPolicy Policy = new(3, TimeSpan.FromSeconds(2), 2.0, TimeSpan.FromSeconds(60));

    /// <summary>The four transition methods of the aggregate, for the terminal-state matrix.</summary>
    public enum Operation { MarkRunning, BeginReconnect, Stop, MarkFailed }

    // ------------------------------------------------------------------- creation

    [Fact]
    public void Launch_ValidArguments_StartsInStartingAndEmitsSessionLaunched()
    {
        var profileId = ProfileId.New();
        var channelId = ChannelId.New();

        var session = StreamSession.Launch(profileId, channelId, "C:\\videos\\in.mp4", Policy);

        Assert.Equal(SessionStatus.Starting, session.Status);
        Assert.Equal(0, session.ReconnectAttempts);
        Assert.Null(session.LastError);
        Assert.Equal("C:\\videos\\in.mp4", session.InputFilePath);

        var launched = SingleEvent<SessionLaunched>(session);
        Assert.Equal(session.Id, launched.Id);
        Assert.Equal(profileId, launched.ProfileId);
        Assert.Equal(channelId, launched.ChannelId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Launch_BlankInputFilePath_ThrowsDomainException(string? inputFilePath)
        => Assert.Throws<DomainException>(
            () => StreamSession.Launch(ProfileId.New(), ChannelId.New(), inputFilePath!, Policy));

    [Fact]
    public void Launch_NullPolicy_ThrowsDomainException()
        => Assert.Throws<DomainException>(
            () => StreamSession.Launch(ProfileId.New(), ChannelId.New(), "in.mp4", null!));

    // ------------------------------------------------------------ maximum duration
    //
    // The session carries the INTENTION (ADR-0009): it validates the window the user asked for and
    // remembers it. It never evaluates it — the aggregate has no clock, the coordinator owns the
    // deadline.

    [Fact]
    public void Launch_WithoutAMaxDuration_LeavesTheBroadcastUnbounded()
    {
        var session = StreamSession.Launch(ProfileId.New(), ChannelId.New(), "in.mp4", Policy);

        Assert.Null(session.MaxDuration);
        Assert.Null(session.StopReason);
        Assert.Equal(SessionStatus.Starting, session.Status);   // nothing else changes
    }

    [Fact]
    public void Launch_WithAMaxDuration_KeepsIt()
    {
        var session = StreamSession.Launch(
            ProfileId.New(), ChannelId.New(), "in.mp4", Policy, TimeSpan.FromHours(2));

        Assert.Equal(TimeSpan.FromHours(2), session.MaxDuration);
    }

    [Fact]
    public void Launch_WithExactlyTheMaximumAllowedDuration_IsAccepted()
    {
        // The bound is INCLUSIVE: "24 h" is the value the UI offers as its maximum, and refusing it
        // would refuse the very entry the domain advertises.
        var session = StreamSession.Launch(
            ProfileId.New(), ChannelId.New(), "in.mp4", Policy, StreamSession.MaxAllowedDuration);

        Assert.Equal(StreamSession.MaxAllowedDuration, session.MaxDuration);
    }

    [Theory]
    [InlineData(0)]        // S1: a window of zero means nothing
    [InlineData(-1)]
    [InlineData(-7200)]
    public void Launch_WithANonPositiveMaxDuration_ThrowsDomainException(int seconds)
        => Assert.Throws<DomainException>(() => StreamSession.Launch(
            ProfileId.New(), ChannelId.New(), "in.mp4", Policy, TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Launch_BeyondTheMaximumAllowedDuration_ThrowsDomainException()
    {
        // S2, the typo guard: one second past the bound is refused, so the bound is the bound.
        var tooLong = StreamSession.MaxAllowedDuration + TimeSpan.FromSeconds(1);

        Assert.Throws<DomainException>(() => StreamSession.Launch(
            ProfileId.New(), ChannelId.New(), "in.mp4", Policy, tooLong));
    }

    // --------------------------------------------------------- allowed transitions

    [Fact]
    public void MarkRunning_FromStarting_TransitionsToRunningAndEmitsSessionStarted()
    {
        var session = Starting();

        session.MarkRunning();

        Assert.Equal(SessionStatus.Running, session.Status);
        Assert.Equal(session.Id, SingleEvent<SessionStarted>(session).Id);
    }

    [Fact]
    public void Stop_FromStarting_TransitionsToStoppedAndEmitsSessionStopped()
    {
        var session = Starting();

        session.Stop(SessionStopReason.Manual);

        Assert.Equal(SessionStatus.Stopped, session.Status);
        Assert.Equal(SessionStopReason.Manual, session.StopReason);

        var stopped = SingleEvent<SessionStopped>(session);
        Assert.Equal(session.Id, stopped.Id);
        Assert.Equal(SessionStopReason.Manual, stopped.Reason);
    }

    [Fact]
    public void MarkFailed_FromStarting_GoesStraightToFailedWithoutAnyReconnectAttempt()
    {
        var session = Starting();

        session.MarkFailed("ffmpeg exited unexpectedly (code 1).");

        Assert.Equal(SessionStatus.Failed, session.Status);
        Assert.Equal(0, session.ReconnectAttempts);   // no backoff on an initial failure
        Assert.Equal("ffmpeg exited unexpectedly (code 1).", session.LastError);

        var failed = SingleEvent<SessionFailed>(session);
        Assert.Equal("ffmpeg exited unexpectedly (code 1).", failed.Reason);
    }

    [Fact]
    public void BeginReconnect_FromRunning_TransitionsToReconnectingAndEmitsReconnectStarted()
    {
        var session = Running();

        session.BeginReconnect("stream dropped");

        Assert.Equal(SessionStatus.Reconnecting, session.Status);
        Assert.Equal(1, session.ReconnectAttempts);

        var started = SingleEvent<ReconnectStarted>(session);
        Assert.Equal(1, started.Attempt);
        Assert.Equal(Policy.DelayFor(1), started.NextDelay);
        Assert.Equal("stream dropped", started.Reason);
    }

    [Fact]
    public void Stop_FromRunning_TransitionsToStoppedAndEmitsSessionStopped()
    {
        var session = Running();

        session.Stop(SessionStopReason.Manual);

        Assert.Equal(SessionStatus.Stopped, session.Status);
        Assert.Equal(SessionStopReason.Manual, session.StopReason);

        var stopped = SingleEvent<SessionStopped>(session);
        Assert.Equal(session.Id, stopped.Id);
        Assert.Equal(SessionStopReason.Manual, stopped.Reason);
    }

    [Fact]
    public void MarkFailed_FromRunning_TransitionsToFailedAndEmitsSessionFailedOnly()
    {
        // Explicit giving-up on a live session: an internal fault leaves nothing able to drive it.
        // Not to be confused with an ffmpeg exit, which still goes through BeginReconnect and
        // consumes the reconnection budget (see BeginReconnect_FromRunning_... above).
        var session = Running();

        session.MarkFailed("the coordinator can no longer drive the session");

        Assert.Equal(SessionStatus.Failed, session.Status);
        Assert.Equal(0, session.ReconnectAttempts);   // no reconnection was attempted...
        Assert.Equal("the coordinator can no longer drive the session", session.LastError);

        // ...and none is INVENTED to get there. The events are the audit trail of the session: the
        // aggregate used to refuse this transition, so the coordinator reached Failed by emitting a
        // ReconnectStarted for an attempt that never took place. SingleEvent proves it is gone.
        var failed = SingleEvent<SessionFailed>(session);
        Assert.Equal("the coordinator can no longer drive the session", failed.Reason);
    }

    [Fact]
    public void MarkRunning_FromReconnecting_RecoversResetsAttemptsAndEmitsSessionRecovered()
    {
        var session = Reconnecting();

        session.MarkRunning();

        Assert.Equal(SessionStatus.Running, session.Status);
        Assert.Equal(0, session.ReconnectAttempts);   // the counter restarts after a recovery

        var recovered = SingleEvent<SessionRecovered>(session);
        Assert.Equal(1, recovered.AfterAttempts);
    }

    [Fact]
    public void Stop_FromReconnecting_TransitionsToStoppedAndEmitsSessionStopped()
    {
        var session = Reconnecting();

        session.Stop(SessionStopReason.Manual);

        Assert.Equal(SessionStatus.Stopped, session.Status);
        Assert.Equal(SessionStopReason.Manual, session.StopReason);

        var stopped = SingleEvent<SessionStopped>(session);
        Assert.Equal(session.Id, stopped.Id);
        Assert.Equal(SessionStopReason.Manual, stopped.Reason);
    }

    // ---------------------------------------------------------- automatic stop (ADR-0009)

    [Fact]
    public void Stop_WithDurationElapsed_OnABoundedSession_RecordsTheReasonOnTheSessionAndTheEvent()
    {
        // The reason is an ATTRIBUTE of the stop, not another stop: one transition, one event. It is
        // carried by both the aggregate (what the UI reads) and the event (the audit trail).
        var session = Running(TimeSpan.FromHours(2));

        session.Stop(SessionStopReason.DurationElapsed);

        Assert.Equal(SessionStatus.Stopped, session.Status);
        Assert.Equal(SessionStopReason.DurationElapsed, session.StopReason);
        Assert.Equal(SessionStopReason.DurationElapsed, SingleEvent<SessionStopped>(session).Reason);
    }

    [Fact]
    public void Stop_WithDurationElapsed_FromReconnecting_IsAllowed()
    {
        // Arbitrage D at domain level: the scheduled stop wins over the backoff. The transition
        // already existed — the automatic stop adds NO new arrow to the state machine.
        var session = Reconnecting(TimeSpan.FromHours(2));

        session.Stop(SessionStopReason.DurationElapsed);

        Assert.Equal(SessionStatus.Stopped, session.Status);
        Assert.Equal(SessionStopReason.DurationElapsed, session.StopReason);
    }

    [Fact]
    public void Stop_WithDurationElapsed_OnAnUnboundedSession_ThrowsDomainException()
    {
        // S3, and it is what keeps MaxDuration from being dead data: with no limit there is no
        // duration to elapse, so a trigger reaching this session is a bug — it fails loudly rather
        // than mislabelling an arrest nobody asked for.
        var session = Running();

        Assert.Throws<DomainException>(() => session.Stop(SessionStopReason.DurationElapsed));

        Assert.Equal(SessionStatus.Running, session.Status);   // and the broadcast is untouched
        Assert.Null(session.StopReason);
    }

    [Fact]
    public void StopReason_OnASessionThatWasNeverStopped_IsNull()
    {
        Assert.Null(Starting().StopReason);
        Assert.Null(Running().StopReason);

        // A failure is NOT a stop: MarkFailed leaves the reason empty, and SessionFailed says why.
        var failed = Running();
        failed.MarkFailed("ffmpeg exited unexpectedly (code 1).");

        Assert.Equal(SessionStatus.Failed, failed.Status);
        Assert.Null(failed.StopReason);
    }

    [Fact]
    public void MarkFailed_FromReconnecting_TransitionsToFailedAndEmitsSessionFailed()
    {
        // Documented "attempts exhausted" route out of Reconnecting (ARCHITECTURE.md §2.4).
        var session = Reconnecting();

        session.MarkFailed("reconnection attempts exhausted");

        Assert.Equal(SessionStatus.Failed, session.Status);
        Assert.Equal("reconnection attempts exhausted", session.LastError);
        Assert.Equal("reconnection attempts exhausted", SingleEvent<SessionFailed>(session).Reason);
    }

    // -------------------------------------------------------- forbidden transitions

    [Fact]
    public void BeginReconnect_FromStarting_ThrowsDomainException()
    {
        // An initial failure must not trigger the backoff: the configuration is likely wrong.
        var session = Starting();

        Assert.Throws<DomainException>(() => session.BeginReconnect("stream dropped"));
    }

    [Fact]
    public void MarkRunning_FromRunning_ThrowsDomainException()
        => Assert.Throws<DomainException>(() => Running().MarkRunning());

    [Theory]
    [InlineData(Operation.MarkRunning)]
    [InlineData(Operation.BeginReconnect)]
    [InlineData(Operation.Stop)]
    [InlineData(Operation.MarkFailed)]
    public void AnyTransition_FromStopped_ThrowsDomainException(Operation operation)
        => Assert.Throws<DomainException>(() => Invoke(Stopped(), operation));

    [Theory]
    [InlineData(Operation.MarkRunning)]
    [InlineData(Operation.BeginReconnect)]
    [InlineData(Operation.Stop)]
    [InlineData(Operation.MarkFailed)]
    public void AnyTransition_FromFailed_ThrowsDomainException(Operation operation)
        => Assert.Throws<DomainException>(() => Invoke(Failed(), operation));

    // ------------------------------------------------------ reconnection accounting

    [Fact]
    public void BeginReconnect_AfterARecovery_CountsTheFirstAttemptAgain()
    {
        // Recovering resets the counter, so a Running session always restarts at attempt 1:
        // the attempts only accumulate within a single, uninterrupted reconnection episode.
        var session = Running();

        session.BeginReconnect("first drop");
        session.MarkRunning();
        session.ClearDomainEvents();
        session.BeginReconnect("second drop");

        Assert.Equal(SessionStatus.Reconnecting, session.Status);
        Assert.Equal(1, session.ReconnectAttempts);
        Assert.Equal(1, SingleEvent<ReconnectStarted>(session).Attempt);
    }

    [Fact]
    public void BeginReconnect_FromReconnecting_CountsAnotherAttemptAndStaysReconnecting()
    {
        // A relaunch that dies before producing stats must count a further attempt.
        // The coordinator relies on this (StreamSessionCoordinator, case Reconnecting).
        var session = Reconnecting();               // already at attempt 1

        session.BeginReconnect("relaunch failed");

        Assert.Equal(SessionStatus.Reconnecting, session.Status);
        Assert.Equal(2, session.ReconnectAttempts);
        Assert.Equal(2, SingleEvent<ReconnectStarted>(session).Attempt);
    }

    [Fact]
    public void BeginReconnect_SuccessiveFailedRelaunches_GrowTheBackoffDelay()
    {
        // Policy: 3 attempts, 2s initial, factor 2 => 2s, 4s, 8s.
        var session = Running();

        var delays = new List<TimeSpan>();
        for (var i = 0; i < Policy.MaxAttempts; i++)
        {
            session.ClearDomainEvents();
            session.BeginReconnect($"drop {i}");
            delays.Add(SingleEvent<ReconnectStarted>(session).NextDelay);
        }

        Assert.Equal(SessionStatus.Reconnecting, session.Status);
        Assert.Equal(Policy.MaxAttempts, session.ReconnectAttempts);
        Assert.Equal(
            [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)],
            delays);
    }

    [Fact]
    public void BeginReconnect_WhenAttemptsAreExhausted_TransitionsToFailed()
    {
        // This branch used to be unreachable: the counter never exceeded 1, so a session
        // could stay Reconnecting forever instead of giving up.
        var session = Running();

        for (var i = 0; i < Policy.MaxAttempts; i++)
            session.BeginReconnect($"drop {i}");

        Assert.Equal(SessionStatus.Reconnecting, session.Status);

        session.ClearDomainEvents();
        session.BeginReconnect("the last relaunch failed too");

        Assert.Equal(SessionStatus.Failed, session.Status);
        Assert.Equal("the last relaunch failed too", session.LastError);
        Assert.Equal("the last relaunch failed too", SingleEvent<SessionFailed>(session).Reason);
    }

    [Fact]
    public void MarkRunning_MidEpisode_RestoresTheFullAttemptBudget()
    {
        var session = Running();

        session.BeginReconnect("drop 1");
        session.BeginReconnect("relaunch failed");   // attempt 2 of 3
        session.MarkRunning();                       // recovered

        Assert.Equal(0, session.ReconnectAttempts);

        // A brand new episode may again consume the whole budget without failing.
        for (var i = 0; i < Policy.MaxAttempts; i++)
            session.BeginReconnect($"new drop {i}");

        Assert.Equal(SessionStatus.Reconnecting, session.Status);
        Assert.Equal(Policy.MaxAttempts, session.ReconnectAttempts);
    }

    // ------------------------------------------------------------ event accumulation

    [Fact]
    public void DomainEvents_AcrossTransitions_AccumulateInOrderUntilDrained()
    {
        var session = StreamSession.Launch(ProfileId.New(), ChannelId.New(), "in.mp4", Policy);
        session.MarkRunning();
        session.Stop(SessionStopReason.Manual);

        Assert.Collection(
            session.DomainEvents,
            evt => Assert.IsType<SessionLaunched>(evt),
            evt => Assert.IsType<SessionStarted>(evt),
            evt => Assert.IsType<SessionStopped>(evt));

        session.ClearDomainEvents();

        Assert.Empty(session.DomainEvents);
    }

    // --------------------------------------------------------------------- helpers

    /// <param name="maxDuration">Null = the unbounded broadcast of the tests that predate ADR-0009.</param>
    private static StreamSession Starting(TimeSpan? maxDuration = null)
    {
        var session = StreamSession.Launch(ProfileId.New(), ChannelId.New(), "in.mp4", Policy, maxDuration);
        session.ClearDomainEvents();
        return session;
    }

    private static StreamSession Running(TimeSpan? maxDuration = null)
    {
        var session = Starting(maxDuration);
        session.MarkRunning();
        session.ClearDomainEvents();
        return session;
    }

    private static StreamSession Reconnecting(TimeSpan? maxDuration = null)
    {
        var session = Running(maxDuration);
        session.BeginReconnect("stream dropped");
        session.ClearDomainEvents();
        return session;
    }

    private static StreamSession Stopped()
    {
        var session = Starting();
        session.Stop(SessionStopReason.Manual);
        session.ClearDomainEvents();
        return session;
    }

    private static StreamSession Failed()
    {
        var session = Starting();
        session.MarkFailed("boom");
        session.ClearDomainEvents();
        return session;
    }

    private static void Invoke(StreamSession session, Operation operation)
    {
        switch (operation)
        {
            case Operation.MarkRunning:
                session.MarkRunning();
                break;
            case Operation.BeginReconnect:
                session.BeginReconnect("stream dropped");
                break;
            case Operation.Stop:
                session.Stop(SessionStopReason.Manual);
                break;
            case Operation.MarkFailed:
                session.MarkFailed("boom");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation.");
        }
    }

    private static T SingleEvent<T>(StreamSession session) where T : IDomainEvent
        => Assert.IsType<T>(Assert.Single(session.DomainEvents));
}
