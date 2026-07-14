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

        session.Stop();

        Assert.Equal(SessionStatus.Stopped, session.Status);
        Assert.Equal(session.Id, SingleEvent<SessionStopped>(session).Id);
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

        session.Stop();

        Assert.Equal(SessionStatus.Stopped, session.Status);
        Assert.Equal(session.Id, SingleEvent<SessionStopped>(session).Id);
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

        session.Stop();

        Assert.Equal(SessionStatus.Stopped, session.Status);
        Assert.Equal(session.Id, SingleEvent<SessionStopped>(session).Id);
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

    [Fact]
    public void MarkFailed_FromRunning_ThrowsDomainException()
        => Assert.Throws<DomainException>(() => Running().MarkFailed("boom"));

    [Fact]
    public void BeginReconnect_FromReconnecting_ThrowsDomainException()
    {
        // Current guard: BeginReconnect is only accepted from Running. A relaunch that fails
        // before producing stats therefore cannot count a second attempt from Reconnecting.
        var session = Reconnecting();

        Assert.Throws<DomainException>(() => session.BeginReconnect("relaunch failed"));
    }

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

    // ------------------------------------------------------------ event accumulation

    [Fact]
    public void DomainEvents_AcrossTransitions_AccumulateInOrderUntilDrained()
    {
        var session = StreamSession.Launch(ProfileId.New(), ChannelId.New(), "in.mp4", Policy);
        session.MarkRunning();
        session.Stop();

        Assert.Collection(
            session.DomainEvents,
            evt => Assert.IsType<SessionLaunched>(evt),
            evt => Assert.IsType<SessionStarted>(evt),
            evt => Assert.IsType<SessionStopped>(evt));

        session.ClearDomainEvents();

        Assert.Empty(session.DomainEvents);
    }

    // --------------------------------------------------------------------- helpers

    private static StreamSession Starting()
    {
        var session = StreamSession.Launch(ProfileId.New(), ChannelId.New(), "in.mp4", Policy);
        session.ClearDomainEvents();
        return session;
    }

    private static StreamSession Running()
    {
        var session = Starting();
        session.MarkRunning();
        session.ClearDomainEvents();
        return session;
    }

    private static StreamSession Reconnecting()
    {
        var session = Running();
        session.BeginReconnect("stream dropped");
        session.ClearDomainEvents();
        return session;
    }

    private static StreamSession Stopped()
    {
        var session = Starting();
        session.Stop();
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
                session.Stop();
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
