using System.ComponentModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Nagare.Application.Abstractions;
using Nagare.Application.Streaming;
using Nagare.Domain.Channels;
using Nagare.Domain.Common;
using Nagare.Domain.Profiles;
using Nagare.Domain.Sessions;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.Application.Streaming;

/// <summary>
/// The coordinator as a sequential mailbox (ADR-0008). The runner is a drivable fake and the
/// backoff runs on a FAKE clock: nothing is ever really awaited, so the tests are deterministic
/// and instant even with the realistic 2s/4s/8s policy below.
///
/// The two properties that carry the whole design:
/// <list type="bullet">
/// <item>a stop mid-backoff is served immediately and NO ffmpeg is relaunched afterwards (SPEC §5);</item>
/// <item>a stats line flushed by an already dead process is discarded (stale epoch), so it cannot
/// fake a recovery and refill the attempt budget;</item>
/// <item>the deadline of a bounded broadcast (ADR-0009) SURVIVES a reconnection and wins over the
/// backoff, while a manual stop wins over the deadline.</item>
/// </list>
/// </summary>
public sealed class StreamSessionCoordinatorTests : IAsyncLifetime
{
    private const string InputFile = "C:\\videos\\in.mp4";

    /// <summary>Budget of 3 attempts, delays 2s / 4s / 8s — on the fake clock (see above).</summary>
    private static readonly ReconnectSettings Settings = new()
    {
        MaxAttempts = 3,
        InitialDelaySeconds = 2,
        Factor = 2,
        MaxDelaySeconds = 60
    };

    /// <summary>Budget for the mailbox loop to process a message. Generous: it never gates a green run.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    private readonly FakeStreamProfileRepository _profiles = new();
    private readonly FakeChannelRepository _channels = new();
    private readonly FakeFfmpegCommandBuilder _commandBuilder = new();
    private readonly FakeFfmpegProcessRunnerFactory _runners = new();
    private readonly FakeTimeProvider _time = new();
    private readonly CapturingLogger<StreamSessionCoordinator> _logger = new();
    private readonly StreamSessionCoordinator _coordinator;
    private readonly ProfileId _profileId;
    private readonly ChannelId _channelId;

    public StreamSessionCoordinatorTests()
    {
        var profile = StreamProfile.Create(
            "Spec",
            new EncodingSettings(
                VideoCodec.H264Nvenc, "p2", RateControl.Cbr,
                bitrateKbps: 3000, maxrateKbps: 3000, bufsizeKbps: 3000,
                gopSize: 60, keyintMin: 60, resolution: null, fps: null),
            new AudioSettings(AudioCodec.Aac, bitrateKbps: 128, sampleRateHz: 48000),
            InputOptions.Default);

        var channel = Channel.Create(
            "Twitch", Platform.Twitch, "rtmp://live.twitch.tv/app",
            new FakeStreamKeyProtector().Protect("live_2468_KpH2sAbCdEf"));

        _profiles.Add(profile);
        _channels.Add(channel);
        _profileId = profile.Id;
        _channelId = channel.Id;

        _coordinator = new StreamSessionCoordinator(
            _profiles,
            _channels,
            _commandBuilder,
            _runners,
            Options.Create(Settings),
            _time,
            _logger);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _coordinator.DisposeAsync();

    // --------------------------------------------------------------------------- start

    [Fact]
    public async Task StartAsync_NoActiveSession_LaunchesFfmpegAndTracksTheSession()
    {
        var id = await StartAsync();

        Assert.Equal(1, _runners.CreateCount);
        Assert.Equal(1, _runners.Runner(1).StartCallCount);
        Assert.True(_coordinator.HasActiveSession);

        var current = _coordinator.Current;
        Assert.NotNull(current);
        Assert.Equal(id, current.Id);
        Assert.Equal(SessionStatus.Starting, current.Status);
        Assert.Equal(0, current.ReconnectAttempts);
    }

    [Fact]
    public async Task StartAsync_WhenASessionIsAlreadyActive_ThrowsAndLeavesTheRunningSessionUntouched()
    {
        var runner = await StartRunningAsync();
        var id = _coordinator.Current!.Id;

        await Assert.ThrowsAsync<DomainException>(
            () => _coordinator.StartAsync(_profileId, _channelId, InputFile, null, CancellationToken.None));

        Assert.Equal(1, _runners.CreateCount);          // no second ffmpeg
        Assert.Equal(0, runner.DisposeCallCount);       // and the live one was not torn down
        Assert.Equal(SessionStatus.Running, _coordinator.Current!.Status);
        Assert.Equal(id, _coordinator.Current!.Id);
    }

    [Fact]
    public async Task StartAsync_UnknownProfile_ThrowsDomainExceptionAndStartsNoFfmpeg()
    {
        await Assert.ThrowsAsync<DomainException>(
            () => _coordinator.StartAsync(ProfileId.New(), _channelId, InputFile, null, CancellationToken.None));

        Assert.Equal(0, _runners.CreateCount);
        Assert.False(_coordinator.HasActiveSession);
    }

    [Fact]
    public async Task StartAsync_UnknownChannel_ThrowsDomainExceptionAndStartsNoFfmpeg()
    {
        await Assert.ThrowsAsync<DomainException>(
            () => _coordinator.StartAsync(_profileId, ChannelId.New(), InputFile, null, CancellationToken.None));

        Assert.Equal(0, _runners.CreateCount);
        Assert.False(_coordinator.HasActiveSession);
    }

    [Fact]
    public async Task StartAsync_WhenFfmpegCannotBeStarted_FailsTheSessionAndPropagates()
    {
        // What Process.Start() throws when the binary is missing, moved or locked by an antivirus.
        _runners.Configure = (runner, _) => runner.StartFailure = new Win32Exception("The system cannot find the file specified.");

        // Re-surfaced as a StreamOperationException rather than the raw Win32Exception: the caller
        // shows this message on screen, so what crosses the boundary must be scrubbed. The type name
        // is kept inside the message — the user still learns what actually failed.
        var surfaced = await Assert.ThrowsAsync<StreamOperationException>(() => StartAsync());
        Assert.Contains("Win32Exception", surfaced.Message, StringComparison.Ordinal);

        var current = _coordinator.Current;
        Assert.NotNull(current);
        Assert.Equal(SessionStatus.Failed, current.Status);          // never left Starting: no zombie
        Assert.Contains("Win32Exception", current.LastError!, StringComparison.Ordinal);
        Assert.False(_coordinator.HasActiveSession);
        Assert.Equal(1, _runners.Runner(1).DisposeCallCount);
    }

    [Fact]
    public async Task StartAsync_WhenTheFailureQuotesTheDestination_ScrubsTheKeyFromWhatReachesTheCaller()
    {
        // The exception handed back to the caller lands in an InfoBar, ON SCREEN. LastError was
        // already scrubbed; THIS is the other path, and it used to propagate the exception raw.
        // No reachable exception embeds the key today — but that is luck, not a guarantee.
        _runners.Configure = (runner, _) => runner.StartFailure = new InvalidOperationException(
            $"ffmpeg refused the destination {FakeFfmpegCommandBuilder.Destination}");

        var surfaced = await Assert.ThrowsAsync<StreamOperationException>(() => StartAsync());

        Assert.DoesNotContain(FakeFfmpegCommandBuilder.StreamKey, surfaced.Message, StringComparison.Ordinal);
        Assert.Contains(ProtectedStreamKey.Mask, surfaced.Message, StringComparison.Ordinal);

        // ToString() as well: one careless interpolation in a future view would print the lot.
        Assert.DoesNotContain(FakeFfmpegCommandBuilder.StreamKey, surfaced.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WithADomainViolation_KeepsTheDomainExceptionType()
    {
        // A DomainException must NOT be wrapped: its message is ours, it carries no secret, and the
        // UI relies on its TYPE to tell a validation error apart from an infrastructure failure.
        await StartRunningAsync();

        await Assert.ThrowsAsync<DomainException>(() => StartAsync());   // a session is already active
    }

    [Fact]
    public async Task StartAsync_AfterAPreviousSession_StartsFromAnEmptyLogBuffer()
    {
        var runner = await StartRunningAsync();
        runner.EmitOutputLine("frame=  120 fps= 30 speed=1.0x");
        Assert.Single(_coordinator.RecentLogs(100));

        await _coordinator.StopAsync(CancellationToken.None);
        await StartAsync();

        // A new session used to display the lines of the previous one (ADR-0008, lifecycle leak).
        Assert.Empty(_coordinator.RecentLogs(100));
    }

    [Fact]
    public async Task StartAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        await _coordinator.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => StartAsync());
    }

    // ---------------------------------------------------------------------- stats flow

    [Fact]
    public async Task StatsReceived_FirstStatsAfterStart_TransitionsToRunning()
    {
        await StartAsync();

        _runners.Runner(1).EmitStats(Stats());
        await WaitForStatusAsync(SessionStatus.Running);

        Assert.True(_coordinator.HasActiveSession);
    }

    [Fact]
    public async Task StatsReceived_FromTheRelaunchedRunner_RecoversTheSessionAndRestoresTheBudget()
    {
        var runner = await StartRunningAsync();

        runner.EmitExit(1);
        await WaitForArmedBackoffAsync(attempts: 1);

        _time.Advance(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => _runners.CreateCount == 2, "ffmpeg to be relaunched");

        _runners.Runner(2).EmitStats(Stats());
        await WaitForStatusAsync(SessionStatus.Running);

        Assert.Equal(0, _coordinator.Current!.ReconnectAttempts);   // a recovery refills the budget
    }

    // ----------------------------------------------------------------------- resilience

    [Fact]
    public async Task ProcessExited_WhileStarting_FailsTheSessionWithoutAnyBackoff()
    {
        // Design rule (§2.4): an initial failure means the configuration is wrong — do not retry.
        await StartAsync();

        _runners.Runner(1).EmitExit(1);
        await WaitForStatusAsync(SessionStatus.Failed);

        Assert.Equal(0, _coordinator.Current!.ReconnectAttempts);
        Assert.Contains("code 1", _coordinator.Current!.LastError!, StringComparison.Ordinal);

        _time.Advance(TimeSpan.FromMinutes(10));
        await FlushAsync();

        Assert.Equal(1, _runners.CreateCount);
    }

    [Fact]
    public async Task ProcessExited_WhileRunning_ReconnectsAfterTheBackoffWindow()
    {
        var runner = await StartRunningAsync();

        runner.EmitExit(1);
        await WaitForArmedBackoffAsync(attempts: 1);

        Assert.Equal(1, runner.DisposeCallCount);   // the dead runner is disposed before the relaunch
        Assert.Equal(1, _runners.CreateCount);      // and nothing is relaunched during the window

        _time.Advance(TimeSpan.FromSeconds(2));     // policy: 2s before attempt 1
        await WaitUntilAsync(() => _runners.CreateCount == 2, "ffmpeg to be relaunched");

        Assert.Equal(1, _runners.Runner(2).StartCallCount);
        Assert.Equal(SessionStatus.Reconnecting, _coordinator.Current!.Status);   // until stats prove otherwise
    }

    [Fact]
    public async Task ProcessExited_SuccessiveFailedRelaunches_IncrementTheAttemptsWithAGrowingDelay()
    {
        var runner = await StartRunningAsync();

        runner.EmitExit(1);
        await WaitForArmedBackoffAsync(attempts: 1);

        // Attempt 1 is due after 2s.
        _time.Advance(TimeSpan.FromMilliseconds(1900));
        await FlushAsync();
        Assert.Equal(1, _runners.CreateCount);

        _time.Advance(TimeSpan.FromMilliseconds(100));
        await WaitUntilAsync(() => _runners.CreateCount == 2, "the first relaunch");

        // The relaunched process dies before producing any stats: another attempt is counted.
        _runners.Runner(2).EmitExit(1);
        await WaitForArmedBackoffAsync(attempts: 2);

        // Attempt 2 is due after 4s, not 2s: a constant delay would relaunch inside this window.
        _time.Advance(TimeSpan.FromMilliseconds(3900));
        await FlushAsync();
        Assert.Equal(2, _runners.CreateCount);

        _time.Advance(TimeSpan.FromMilliseconds(100));
        await WaitUntilAsync(() => _runners.CreateCount == 3, "the second relaunch");
    }

    [Fact]
    public async Task ProcessExited_WhenTheAttemptsAreExhausted_FailsTheSession()
    {
        var runner = await StartRunningAsync();
        runner.EmitExit(1);

        // Budget of 3: attempts 1, 2 and 3 are relaunched, the 4th drop gives up.
        for (var attempt = 1; attempt <= Settings.MaxAttempts; attempt++)
        {
            await WaitForArmedBackoffAsync(attempt);

            _time.Advance(Settings.ToPolicy().DelayFor(attempt));
            await WaitUntilAsync(() => _runners.CreateCount == attempt + 1, $"relaunch number {attempt}");

            _runners.Runner(attempt + 1).EmitExit(1);
        }

        await WaitForStatusAsync(SessionStatus.Failed);

        Assert.False(_coordinator.HasActiveSession);
        Assert.Contains("code 1", _coordinator.Current!.LastError!, StringComparison.Ordinal);
        Assert.Equal(4, _runners.CreateCount);                          // 1 launch + 3 relaunches
        Assert.Equal(1, _runners.Runner(4).DisposeCallCount);           // nothing is left running

        _time.Advance(TimeSpan.FromMinutes(10));
        await FlushAsync();
        Assert.Equal(4, _runners.CreateCount);
    }

    [Fact]
    public async Task ReconnectDue_WhenFfmpegCannotBeRelaunched_FailsTheSessionInsteadOfLeavingAZombie()
    {
        // The binary vanished between the launch and the relaunch: StartAsync throws, no runner is
        // left to ever raise Exited again. The session used to stay Reconnecting forever.
        _runners.Configure = (runner, launchNumber) =>
        {
            if (launchNumber == 2)
                runner.StartFailure = new Win32Exception("The system cannot find the file specified.");
        };

        var runner = await StartRunningAsync();
        runner.EmitExit(1);
        await WaitForArmedBackoffAsync(attempts: 1);

        _time.Advance(TimeSpan.FromSeconds(2));
        await WaitForStatusAsync(SessionStatus.Failed);

        Assert.False(_coordinator.HasActiveSession);
        Assert.Contains("Win32Exception", _coordinator.Current!.LastError!, StringComparison.Ordinal);
        Assert.Equal(1, _runners.Runner(2).DisposeCallCount);
    }

    [Fact]
    public async Task StatsReceived_FromADeadRunnerAfterItsExit_IsIgnoredAndKeepsTheAttemptCount()
    {
        var runner = await StartRunningAsync();

        runner.EmitExit(1);
        await WaitForArmedBackoffAsync(attempts: 1);

        // The stderr readers flush their buffer asynchronously: a progression line of the process
        // that just died lands now, through the handler they captured while it was alive. Its epoch
        // is stale, so the loop must discard it — otherwise it fakes a SessionRecovered, resets the
        // counter, and the attempt budget is never exhausted: the session never reaches Failed.
        runner.EmitTrailingStats(Stats());

        // The mailbox is FIFO: when the relaunch below happens, the stale stats have been processed.
        _time.Advance(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => _runners.CreateCount == 2, "ffmpeg to be relaunched");

        var current = _coordinator.Current!;
        Assert.Equal(SessionStatus.Reconnecting, current.Status);   // no false recovery
        Assert.Equal(1, current.ReconnectAttempts);                 // budget NOT refilled
    }

    [Fact]
    public async Task ProcessExited_WithTrailingStatsFromEveryDeadRunner_StillExhaustsTheAttemptsAndFails()
    {
        // The consequence that mattered: a stale stats line refilled the attempt budget, so the
        // budget was never exhausted and a permanently broken stream never reached Failed — it
        // relaunched ffmpeg forever. Here every dead process flushes a line, and the session must
        // still give up after exactly MaxAttempts relaunches.
        var runner = await StartRunningAsync();
        runner.EmitExit(1);
        runner.EmitTrailingStats(Stats());

        for (var attempt = 1; attempt <= Settings.MaxAttempts; attempt++)
        {
            await WaitForArmedBackoffAsync(attempt);

            _time.Advance(Settings.ToPolicy().DelayFor(attempt));
            await WaitUntilAsync(() => _runners.CreateCount == attempt + 1, $"relaunch number {attempt}");

            var relaunched = _runners.Runner(attempt + 1);
            relaunched.EmitExit(1);
            relaunched.EmitTrailingStats(Stats());
        }

        await WaitForStatusAsync(SessionStatus.Failed);

        Assert.Equal(4, _runners.CreateCount);   // 1 launch + 3 relaunches, then it gives up
    }

    // ----------------------------------------------------------------------------- stop

    [Fact]
    public async Task StopAsync_DuringTheBackoffWindow_IsServedImmediatelyAndRelaunchesNoFfmpeg()
    {
        // THE test of SPEC §5. The clock is frozen: a loop that awaited the backoff would never
        // even read this stop message, and the call below would time out.
        var runner = await StartRunningAsync();

        runner.EmitExit(1);
        await WaitForArmedBackoffAsync(attempts: 1);   // the backoff is really pending when the stop lands

        await _coordinator.StopAsync(CancellationToken.None).WaitAsync(Budget);

        Assert.Equal(SessionStatus.Stopped, _coordinator.Current!.Status);
        Assert.False(_coordinator.HasActiveSession);

        // Well past every backoff window: nothing may ever be published again after a stop.
        _time.Advance(TimeSpan.FromMinutes(10));
        await FlushAsync();

        Assert.Equal(1, _runners.CreateCount);
        Assert.Equal(1, runner.DisposeCallCount);
    }

    [Fact]
    public async Task StopAsync_WithAReconnectDueAlreadyQueued_RelaunchesNoFfmpeg()
    {
        // The one window the epoch cannot close: the backoff delay elapses JUST before the stop is
        // posted, so its ReconnectDue sits AHEAD of the stop in the FIFO, still under the current
        // epoch. The loop would relaunch ffmpeg — the stream reappears on Twitch — and kill it
        // milliseconds later. StopAsync therefore cancels the session token BEFORE posting.
        //
        // Determinism: the loop is held inside the dispose of the dead runner, so it cannot consume
        // the mailbox while the test arranges exactly the queue it wants — [ReconnectDue, Stop].
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = await StartRunningAsync();
        runner.DisposeBlocker = gate.Task;

        runner.EmitExit(1);
        await WaitForAttemptAsync(SessionStatus.Reconnecting, attempts: 1);
        await runner.DisposeEntered.WaitAsync(Budget);   // backoff armed, and the loop is now held

        _time.Advance(TimeSpan.FromSeconds(2));          // ReconnectDue is written to the mailbox now

        var stop = _coordinator.StopAsync(CancellationToken.None);   // cancels, THEN posts behind it
        gate.SetResult();                                            // the loop resumes: ReconnectDue first

        await stop.WaitAsync(Budget);

        // The conclusion is SUBORDINATED to the proof that barrier 3 actually fired: the
        // ReconnectDue was consumed AND abandoned. Asserting CreateCount alone proved hollow —
        // an innocuous reorder of the exit handler (cleanup before scheduling) starves the fake
        // timer of its Advance, no ReconnectDue is ever posted, and the count passes for the
        // wrong reason with the barrier deleted (found by the review's mutation run).
        Assert.Equal(1, _logger.CountOf(StreamSessionCoordinator.StopAbortedReconnectLogMessage));

        Assert.Equal(1, _runners.CreateCount);   // the relaunch gave up: no ffmpeg was ever put back on air
        Assert.Equal(SessionStatus.Stopped, _coordinator.Current!.Status);
        Assert.False(_coordinator.HasActiveSession);
    }

    [Fact]
    public async Task StopAsync_WhileRunning_StopsAndDisposesTheRunner()
    {
        var runner = await StartRunningAsync();

        await _coordinator.StopAsync(CancellationToken.None);

        Assert.Equal(1, runner.StopCallCount);
        Assert.Equal(1, runner.DisposeCallCount);
        Assert.Equal(SessionStatus.Stopped, _coordinator.Current!.Status);
        Assert.Equal(SessionStopReason.Manual, _coordinator.Current!.StopReason);
        Assert.False(_coordinator.HasActiveSession);
    }

    [Fact]
    public async Task StopAsync_WithoutAnyActiveSession_IsANoOp()
    {
        await _coordinator.StopAsync(CancellationToken.None);

        Assert.Null(_coordinator.Current);
        Assert.Equal(0, _runners.CreateCount);
    }

    [Fact]
    public async Task StopAsync_OnAnAlreadyStoppedSession_IsANoOp()
    {
        var runner = await StartRunningAsync();
        await _coordinator.StopAsync(CancellationToken.None);

        await _coordinator.StopAsync(CancellationToken.None);

        Assert.Equal(1, runner.StopCallCount);
        Assert.Equal(SessionStatus.Stopped, _coordinator.Current!.Status);
    }

    [Fact]
    public async Task HostedServiceStopAsync_WithAnActiveSession_KillsFfmpeg()
    {
        // SPEC §5: closing the app must never leave an ffmpeg process publishing.
        var runner = await StartRunningAsync();

        await ((IHostedService)_coordinator).StopAsync(CancellationToken.None);

        Assert.Equal(1, runner.StopCallCount);
        Assert.Equal(1, runner.DisposeCallCount);
        Assert.Equal(SessionStatus.Stopped, _coordinator.Current!.Status);
    }

    [Fact]
    public async Task DisposeAsync_WithAnActiveSession_DisposesTheRunner()
    {
        var runner = await StartRunningAsync();

        await _coordinator.DisposeAsync();

        Assert.Equal(1, runner.DisposeCallCount);   // the process is killed by the runner's dispose
    }

    [Fact]
    public async Task DisposeAsync_WhenTheLoopIsStuck_GivesUpAfterTheTimeoutAndStillKillsFfmpeg()
    {
        // The loop always ends by construction — but "by construction" is an argument, not a
        // guarantee. If a handler ever hung, an unbounded wait would freeze the shutdown of the app
        // AND leave ffmpeg publishing, which is the exact opposite of what SPEC §5 demands. Here the
        // runner never returns from StartAsync: the wait must give up and kill the process anyway.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _runners.Configure = (runner, _) => runner.StartBlocker = gate.Task;

        var start = StartAsync();   // never completes: the loop is stuck inside the runner
        await WaitUntilAsync(() => _runners.CreateCount == 1, "ffmpeg to be launched");
        await _runners.Runner(1).StartEntered.WaitAsync(Budget);

        var dispose = _coordinator.DisposeAsync().AsTask();

        try
        {
            // The 5s bound is counted on the coordinator's clock, so nothing is really awaited here.
            await AdvanceUntilCompletedAsync(dispose, "the bounded shutdown to give up on the loop");
        }
        finally
        {
            gate.TrySetResult();   // release the loop whatever happened, so the teardown can end
        }

        Assert.Equal(1, _runners.Runner(1).DisposeCallCount);   // ffmpeg killed despite the stuck loop
        await start.WaitAsync(Budget);                          // and the caller is never left hanging
    }

    // ----------------------------------------------------------------- maximum duration
    //
    // US-0 / ADR-0009. The deadline is armed OUTSIDE the loop on the fake clock, exactly like the
    // backoff: no test below waits a single real millisecond, even for a 48-hour window.

    [Fact]
    public async Task DurationLimit_WhenNoDurationWasGiven_NeverStopsTheSession()
    {
        // A broadcast without a limit is the path of today, untouched: no deadline, no message.
        await StartRunningAsync();

        Assert.Null(_coordinator.Current!.PlannedEndsAt);

        _time.Advance(TimeSpan.FromHours(48));
        await FlushAsync();

        Assert.Equal(SessionStatus.Running, _coordinator.Current!.Status);
        Assert.Null(_coordinator.Current!.PlannedEndsAt);
        Assert.Equal(1, _runners.CreateCount);
    }

    [Fact]
    public async Task StartAsync_WithAMaxDuration_PublishesThePlannedEndFromTheVeryFirstSnapshot()
    {
        // US-0 asks for an end time to be DISPLAYED. It must ride on the first snapshot: waiting for
        // the next transition would leave a bounded broadcast with no end in sight on screen.
        var snapshots = new List<SessionSnapshot>();
        _coordinator.Changed += snapshots.Add;

        var launchedAt = _time.GetUtcNow();
        await StartAsync(TimeSpan.FromHours(2));

        Assert.Equal(launchedAt + TimeSpan.FromHours(2), snapshots[0].PlannedEndsAt);
        Assert.Equal(launchedAt + TimeSpan.FromHours(2), _coordinator.Current!.PlannedEndsAt);
    }

    [Fact]
    public async Task DurationLimit_JustBeforeTheDeadline_LeavesTheBroadcastOnAir()
    {
        await StartRunningAsync(TimeSpan.FromSeconds(10));

        _time.Advance(TimeSpan.FromSeconds(10) - TimeSpan.FromMilliseconds(1));
        await FlushAsync();

        Assert.Equal(SessionStatus.Running, _coordinator.Current!.Status);
        Assert.Null(_coordinator.Current!.StopReason);
    }

    [Fact]
    public async Task DurationLimit_WhenTheDeadlineElapsesWhileRunning_StopsTheSessionAndKillsFfmpeg()
    {
        var runner = await StartRunningAsync(TimeSpan.FromSeconds(10));

        _time.Advance(TimeSpan.FromSeconds(10));
        await WaitForStatusAsync(SessionStatus.Stopped);

        // Same ending as a manual stop — only the label differs, and it is what the UI reports as
        // "arrêt automatique (durée atteinte)".
        Assert.Equal(SessionStopReason.DurationElapsed, _coordinator.Current!.StopReason);
        Assert.Equal(1, runner.StopCallCount);
        Assert.Equal(1, runner.DisposeCallCount);
        Assert.False(_coordinator.HasActiveSession);
    }

    [Fact]
    public async Task DurationLimit_WhenTheDeadlineElapsesWhileReconnecting_AbandonsTheBackoff()
    {
        // Arbitrage D. The deadline falls one second BEFORE the pending relaunch: the backoff is
        // abandoned and no ffmpeg is ever put back on air. The user asked for one second of
        // broadcast; retrying past it contradicts the only instruction they gave.
        var runner = await StartRunningAsync(TimeSpan.FromSeconds(1));

        runner.EmitExit(1);
        await WaitForArmedBackoffAsync(attempts: 1);   // relaunch due at t0 + 2s

        _time.Advance(TimeSpan.FromSeconds(1));
        await WaitForStatusAsync(SessionStatus.Stopped);

        Assert.Equal(SessionStopReason.DurationElapsed, _coordinator.Current!.StopReason);

        // Well past every backoff window: the abandoned attempt must never come back.
        _time.Advance(TimeSpan.FromMinutes(10));
        await FlushAsync();

        Assert.Equal(1, _runners.CreateCount);
        Assert.False(_coordinator.HasActiveSession);
    }

    [Fact]
    public async Task DurationLimit_AfterAReconnection_StillStopsAtTheOriginalDeadline()
    {
        // THE test that rejects an epoch-tagged deadline. The epoch is a RUNNER generation and
        // changes at every ffmpeg exit: tagged with it, this message would be declared stale from
        // the first reconnection on, and the automatic stop would simply never happen.
        var runner = await StartRunningAsync(TimeSpan.FromSeconds(6));

        runner.EmitExit(1);
        await WaitForArmedBackoffAsync(attempts: 1);

        _time.Advance(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => _runners.CreateCount == 2, "ffmpeg to be relaunched");

        _runners.Runner(2).EmitStats(Stats());
        await WaitForStatusAsync(SessionStatus.Running);   // recovered, under a NEW epoch

        _time.Advance(TimeSpan.FromSeconds(4));            // t0 + 6s: the deadline of the LAUNCH
        await WaitForStatusAsync(SessionStatus.Stopped);

        Assert.Equal(SessionStopReason.DurationElapsed, _coordinator.Current!.StopReason);
    }

    [Fact]
    public async Task StopAsync_BeforeTheDeadline_StopsManuallyAndKeepsThatReason()
    {
        // "Stopping before the end time works as it does today" (US-0): the duration prevents
        // nothing, and the report says the user stopped it — not the clock.
        var runner = await StartRunningAsync(TimeSpan.FromHours(2));

        await _coordinator.StopAsync(CancellationToken.None);

        Assert.Equal(SessionStopReason.Manual, _coordinator.Current!.StopReason);

        _time.Advance(TimeSpan.FromHours(3));   // past the deadline of a session that is already over
        await FlushAsync();

        Assert.Equal(SessionStatus.Stopped, _coordinator.Current!.Status);
        Assert.Equal(SessionStopReason.Manual, _coordinator.Current!.StopReason);
        Assert.Equal(1, runner.StopCallCount);
    }

    [Fact]
    public async Task DurationLimit_OnASessionThatAlreadyFailed_IsIgnoredWithoutAnyError()
    {
        // EndSessionAsync cancels the deadline, but not before the runner is reaped — and killing a
        // process can take a while. A deadline elapsing inside that window has ALREADY posted its
        // message. Without the freshness guard, Stop() on a terminal session throws a
        // DomainException all the way up to the loop's last-resort catch: this is that window,
        // reproduced deterministically by holding the loop inside the dispose.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _runners.Configure = (runner, _) => runner.DisposeBlocker = gate.Task;

        var duration = TimeSpan.FromSeconds(10);
        await StartAsync(duration);

        _runners.Runner(1).EmitExit(1);                     // dies while Starting -> Failed, no backoff
        await WaitForStatusAsync(SessionStatus.Failed);
        await _runners.Runner(1).DisposeEntered.WaitAsync(Budget);   // the loop is now held

        _time.Advance(duration);                            // DurationElapsed is queued behind it
        gate.SetResult();

        await FlushAsync();

        var current = _coordinator.Current!;
        Assert.Equal(SessionStatus.Failed, current.Status);
        Assert.Contains("code 1", current.LastError!, StringComparison.Ordinal);
        Assert.Null(current.StopReason);                    // a failure is not a stop
        Assert.DoesNotContain(_logger.Messages, m => m.Contains("Unhandled error", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3600)]
    public async Task StartAsync_WithANonPositiveMaxDuration_IsRefusedAndStartsNoFfmpeg(int seconds)
    {
        // The bound is a domain invariant, not a preflight rule: it is checked before anything is
        // launched, so an impossible window never puts a single frame on air.
        await Assert.ThrowsAsync<DomainException>(() => StartAsync(TimeSpan.FromSeconds(seconds)));

        Assert.Equal(0, _runners.CreateCount);
        Assert.False(_coordinator.HasActiveSession);
    }

    [Fact]
    public async Task StopAsync_WithADurationElapsedAlreadyQueued_KeepsTheManualReason()
    {
        // Barrier 3, on the automatic stop this time: the deadline elapses JUST before the stop is
        // posted, so its message sits AHEAD of it in the FIFO, and the session is still active. The
        // broadcast ends either way — what is at stake is the LABEL, and it belongs to whoever
        // clicked. The log assertion is what proves the barrier fired: "the session is Stopped" is
        // true with or without it.
        //
        // Determinism: the loop is held inside the runner's start, so it cannot consume the mailbox
        // while the test arranges exactly the queue it wants — [DurationElapsed, Stop].
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _runners.Configure = (runner, _) => runner.StartBlocker = gate.Task;

        var duration = TimeSpan.FromSeconds(10);
        var start = StartAsync(duration);                            // deadline armed, then held

        await WaitUntilAsync(() => _runners.CreateCount == 1, "ffmpeg to be launched");
        await _runners.Runner(1).StartEntered.WaitAsync(Budget);

        _time.Advance(duration);                                     // DurationElapsed written now

        var stop = _coordinator.StopAsync(CancellationToken.None);   // cancels, THEN posts behind it
        gate.SetResult();                                            // the loop resumes: deadline first

        await start.WaitAsync(Budget);
        await stop.WaitAsync(Budget);

        Assert.Equal(1, _logger.CountOf(StreamSessionCoordinator.StopSupersededDurationLogMessage));
        Assert.Equal(SessionStatus.Stopped, _coordinator.Current!.Status);
        Assert.Equal(SessionStopReason.Manual, _coordinator.Current!.StopReason);
    }

    // --------------------------------------------------------------- monitoring & health

    [Fact]
    public async Task Current_WithHealthyStats_ReportsOk()
    {
        var runner = await StartRunningAsync();

        var stats = Stats(speed: 1.0, droppedFrames: 0);
        await EmitAndWaitAsync(runner, stats);

        Assert.Equal(HealthIndicator.Ok, _coordinator.Current!.Health);
        Assert.Equal(stats, _coordinator.Current!.Stats);
    }

    [Fact]
    public async Task Current_WithSpeedBelowRealTime_ReportsAWarning()
    {
        var runner = await StartRunningAsync();

        await EmitAndWaitAsync(runner, Stats(speed: 0.85));

        Assert.Equal(HealthIndicator.Warning, _coordinator.Current!.Health);
    }

    [Fact]
    public async Task Current_WithGrowingDroppedFrames_ReportsAWarning()
    {
        // SPEC §6 announces "speed < 1.0 OR growing drops"; only the speed used to be checked.
        var runner = await StartRunningAsync();

        await EmitAndWaitAsync(runner, Stats(speed: 1.0, droppedFrames: 0));
        Assert.Equal(HealthIndicator.Ok, _coordinator.Current!.Health);

        await EmitAndWaitAsync(runner, Stats(speed: 1.0, droppedFrames: 12));
        Assert.Equal(HealthIndicator.Warning, _coordinator.Current!.Health);

        // Drops that stopped growing are no longer a warning: the health reflects the present.
        await EmitAndWaitAsync(runner, Stats(speed: 1.0, droppedFrames: 12));
        Assert.Equal(HealthIndicator.Ok, _coordinator.Current!.Health);
    }

    [Fact]
    public async Task Current_AfterAReconnection_JudgesHealthOnTheNewProcessAlone()
    {
        // HealthOf compares dropped-frame counters between two samples. The relaunched ffmpeg
        // restarts its counters at ZERO: keeping the dead process's figures would make a real burst
        // of drops read as an improvement — a Warning silently downgraded to Ok.
        var runner = await StartRunningAsync();
        await EmitAndWaitAsync(runner, Stats(speed: 1.0, droppedFrames: 900));   // the corpse dropped a lot

        runner.EmitExit(1);
        await WaitForArmedBackoffAsync(attempts: 1);
        _time.Advance(TimeSpan.FromSeconds(2));
        await WaitForStatusAsync(SessionStatus.Reconnecting);

        // The relaunched process drops from its VERY FIRST sample — the dangerous case. Comparing
        // its 30 to the corpse's 900 reads "fewer drops than before" and reports a healthy stream
        // while frames are being lost. Only a cleared baseline sees 30 > 0 for what it is.
        //
        // A first sample at 0 drops would have masked the bug: it resets the baseline on its own,
        // and the test would pass either way. It did — that flaw was caught by mutating the fix out.
        var relaunched = await WaitForRunnerAsync(2);
        await EmitAndWaitAsync(relaunched, Stats(speed: 1.0, droppedFrames: 30));

        Assert.Equal(HealthIndicator.Warning, _coordinator.Current!.Health);
    }

    // ------------------------------------------------------------ resilience to the UI

    [Fact]
    public async Task Changed_WhenASubscriberThrows_DoesNotTakeTheLiveSessionDown()
    {
        // Subscribers run ON the mailbox loop thread. A WinUI view model throwing while marshalling
        // to the dispatcher must NEVER kill a live broadcast: a broken UI is not a reason to drop
        // the stream. Without isolation the exception bubbles into the loop's catch and fails it.
        var runner = await StartRunningAsync();

        _coordinator.Changed += _ => throw new InvalidOperationException("the view model blew up");

        await EmitAndWaitAsync(runner, Stats(speed: 1.0));

        Assert.Equal(SessionStatus.Running, _coordinator.Current!.Status);
        Assert.True(_coordinator.HasActiveSession);

        // And the session is still alive enough to be stopped normally.
        await _coordinator.StopAsync(CancellationToken.None).WaitAsync(Budget);
        Assert.Equal(SessionStatus.Stopped, _coordinator.Current!.Status);
    }

    [Fact]
    public async Task LogAppended_WhenASubscriberThrows_DoesNotTakeTheLiveSessionDown()
    {
        var runner = await StartRunningAsync();

        _coordinator.LogAppended += _ => throw new InvalidOperationException("the log view blew up");

        runner.EmitOutputLine("frame=  120 fps= 30");
        await EmitAndWaitAsync(runner, Stats(speed: 1.0));

        Assert.Equal(SessionStatus.Running, _coordinator.Current!.Status);
        Assert.True(_coordinator.HasActiveSession);
    }

    // ----------------------------------------------------------------- key never leaks

    [Fact]
    public async Task ReconnectDue_WhenTheFailureQuotesTheDestination_ScrubsTheKeyFromTheReason()
    {
        // ffmpeg echoes the full destination URL — stream key included — in its error messages.
        // The failure reason lands in LastError and SessionFailed, both shown in the UI. It MUST be
        // scrubbed. Today no reachable exception carries the arguments, so the invariant held by
        // LUCK; this test turns luck into a guarantee.
        _runners.Configure = (runner, launchNumber) =>
        {
            if (launchNumber == 2)
                runner.StartFailure = new InvalidOperationException(
                    $"ffmpeg refused the destination {FakeFfmpegCommandBuilder.Destination}");
        };

        var runner = await StartRunningAsync();
        runner.EmitExit(1);
        await WaitForArmedBackoffAsync(attempts: 1);

        _time.Advance(TimeSpan.FromSeconds(2));
        await WaitForStatusAsync(SessionStatus.Failed);

        var reason = _coordinator.Current!.LastError!;
        Assert.DoesNotContain(FakeFfmpegCommandBuilder.StreamKey, reason, StringComparison.Ordinal);
        Assert.Contains(ProtectedStreamKey.Mask, reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecentLogs_AfterOutputLines_ReturnsThemInOrderAndRaisesLogAppended()
    {
        var runner = await StartRunningAsync();
        var appended = new List<string>();
        _coordinator.LogAppended += line => appended.Add(line);

        runner.EmitOutputLine("line 1");
        runner.EmitOutputLine("line 2");

        Assert.Equal(["line 1", "line 2"], _coordinator.RecentLogs(10));
        Assert.Equal(["line 1", "line 2"], appended);
        Assert.Equal(["line 2"], _coordinator.RecentLogs(1));
        Assert.Empty(_coordinator.RecentLogs(0));
    }

    // -------------------------------------------------------------------------- helpers

    /// <param name="maxDuration">Null = the unbounded broadcast: no deadline is armed at all.</param>
    private Task<SessionId> StartAsync(TimeSpan? maxDuration = null)
        => _coordinator.StartAsync(_profileId, _channelId, InputFile, maxDuration, CancellationToken.None);

    /// <summary>Starts a session and brings it to <see cref="SessionStatus.Running"/> via a first stats line.</summary>
    private async Task<FakeFfmpegProcessRunner> StartRunningAsync(TimeSpan? maxDuration = null)
    {
        await StartAsync(maxDuration);

        var runner = _runners.Runner(1);
        runner.EmitStats(Stats());
        await WaitForStatusAsync(SessionStatus.Running);

        return runner;
    }

    private async Task EmitAndWaitAsync(FakeFfmpegProcessRunner runner, FfmpegStats stats)
    {
        runner.EmitStats(stats);
        await WaitUntilAsync(
            () => ReferenceEquals(_coordinator.Current?.Stats, stats),
            "the stats to reach the snapshot");
    }

    private Task WaitForStatusAsync(SessionStatus status)
        => WaitUntilAsync(() => _coordinator.Current?.Status == status, $"the session to reach {status}");

    /// <summary>Waits until the Nth runner has been created, then hands it over.</summary>
    private async Task<FakeFfmpegProcessRunner> WaitForRunnerAsync(int launchNumber)
    {
        await WaitUntilAsync(() => _runners.CreateCount >= launchNumber, $"runner #{launchNumber} to be created");
        return _runners.Runner(launchNumber);
    }

    private Task WaitForAttemptAsync(SessionStatus status, int attempts)
        => WaitUntilAsync(
            () => _coordinator.Current is { } snapshot && snapshot.Status == status && snapshot.ReconnectAttempts == attempts,
            $"the session to reach {status} at attempt {attempts}");

    /// <summary>
    /// Waits for the reconnection episode to be entirely set up: the session at <paramref name="attempts"/>,
    /// AND the loop done with the message that got it there.
    ///
    /// The second half is not a nicety, it is what makes every clock-driven test below deterministic.
    /// The snapshot turns Reconnecting at the TOP of the exit handler; the backoff timer is armed a
    /// few instructions later. A fake-clock timer registered AFTER an Advance counts its due time
    /// from the NEW now — the advance is simply lost, the timer never fires, and the relaunch never
    /// comes. Without this barrier the suite failed roughly once in eight runs, on whichever test
    /// lost the race. The mailbox is FIFO with a single reader, so a probe message processed here
    /// proves the exit handler ran to completion, timer included.
    /// </summary>
    private async Task WaitForArmedBackoffAsync(int attempts)
    {
        await WaitForAttemptAsync(SessionStatus.Reconnecting, attempts);
        await FlushAsync();
    }

    /// <summary>
    /// Polls the mailbox loop until it has produced the expected effect. The real delay here only
    /// paces the polling — the coordinator's backoff runs on the fake clock and never elapses on
    /// its own, so no test ever waits for a backoff window.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition, string expectation)
    {
        var deadline = DateTime.UtcNow + Budget;

        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail($"Timed out after {Budget.TotalSeconds:0}s waiting for {expectation}.");

            await Task.Delay(5);
        }
    }

    /// <summary>
    /// Pushes the FAKE clock forward until the task completes. The timeout being waited on is armed
    /// on another thread, so a single Advance could land before the timer exists and be lost: the
    /// clock is nudged until it fires. The real deadline only makes a broken bound fail the test
    /// instead of hanging it — no test ever waits 5 real seconds.
    /// </summary>
    private async Task AdvanceUntilCompletedAsync(Task task, string expectation)
    {
        var deadline = DateTime.UtcNow + Budget;

        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail($"Timed out after {Budget.TotalSeconds:0}s waiting for {expectation}.");

            _time.Advance(TimeSpan.FromSeconds(6));   // past the coordinator's 5s shutdown bound
            await Task.Delay(5);
        }

        await task;
    }

    /// <summary>
    /// Mailbox barrier, for the assertions that must prove that something did NOT happen. The
    /// mailbox has a single reader and preserves order: once a message posted now has been
    /// processed, every message posted before it has been too. Both probes below mutate nothing —
    /// a start is refused outright while a session is active, a stop is a no-op once it is terminal.
    /// </summary>
    private async Task FlushAsync()
    {
        if (_coordinator.HasActiveSession)
            await Assert.ThrowsAsync<DomainException>(
                () => _coordinator.StartAsync(ProfileId.New(), ChannelId.New(), InputFile, null, CancellationToken.None));
        else
            await _coordinator.StopAsync(CancellationToken.None);
    }

    private static FfmpegStats Stats(double speed = 1.0, int droppedFrames = 0)
        => new(
            Frame: 120,
            Fps: 30,
            BitrateKbps: 3000,
            Speed: speed,
            DroppedFrames: droppedFrames,
            DupFrames: 0,
            Time: TimeSpan.FromSeconds(4));
}
