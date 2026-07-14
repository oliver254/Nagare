using System.ComponentModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
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
/// fake a recovery and refill the attempt budget.</item>
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
            NullLogger<StreamSessionCoordinator>.Instance);
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
            () => _coordinator.StartAsync(_profileId, _channelId, InputFile, CancellationToken.None));

        Assert.Equal(1, _runners.CreateCount);          // no second ffmpeg
        Assert.Equal(0, runner.DisposeCallCount);       // and the live one was not torn down
        Assert.Equal(SessionStatus.Running, _coordinator.Current!.Status);
        Assert.Equal(id, _coordinator.Current!.Id);
    }

    [Fact]
    public async Task StartAsync_UnknownProfile_ThrowsDomainExceptionAndStartsNoFfmpeg()
    {
        await Assert.ThrowsAsync<DomainException>(
            () => _coordinator.StartAsync(ProfileId.New(), _channelId, InputFile, CancellationToken.None));

        Assert.Equal(0, _runners.CreateCount);
        Assert.False(_coordinator.HasActiveSession);
    }

    [Fact]
    public async Task StartAsync_UnknownChannel_ThrowsDomainExceptionAndStartsNoFfmpeg()
    {
        await Assert.ThrowsAsync<DomainException>(
            () => _coordinator.StartAsync(_profileId, ChannelId.New(), InputFile, CancellationToken.None));

        Assert.Equal(0, _runners.CreateCount);
        Assert.False(_coordinator.HasActiveSession);
    }

    [Fact]
    public async Task StartAsync_WhenFfmpegCannotBeStarted_FailsTheSessionAndPropagates()
    {
        // What Process.Start() throws when the binary is missing, moved or locked by an antivirus.
        _runners.Configure = (runner, _) => runner.StartFailure = new Win32Exception("The system cannot find the file specified.");

        await Assert.ThrowsAsync<Win32Exception>(() => StartAsync());

        var current = _coordinator.Current;
        Assert.NotNull(current);
        Assert.Equal(SessionStatus.Failed, current.Status);          // never left Starting: no zombie
        Assert.Contains("Win32Exception", current.LastError!, StringComparison.Ordinal);
        Assert.False(_coordinator.HasActiveSession);
        Assert.Equal(1, _runners.Runner(1).DisposeCallCount);
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
        await WaitForAttemptAsync(SessionStatus.Reconnecting, attempts: 1);

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
        await WaitForAttemptAsync(SessionStatus.Reconnecting, attempts: 1);

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
        await WaitForAttemptAsync(SessionStatus.Reconnecting, attempts: 1);

        // Attempt 1 is due after 2s.
        _time.Advance(TimeSpan.FromMilliseconds(1900));
        await FlushAsync();
        Assert.Equal(1, _runners.CreateCount);

        _time.Advance(TimeSpan.FromMilliseconds(100));
        await WaitUntilAsync(() => _runners.CreateCount == 2, "the first relaunch");

        // The relaunched process dies before producing any stats: another attempt is counted.
        _runners.Runner(2).EmitExit(1);
        await WaitForAttemptAsync(SessionStatus.Reconnecting, attempts: 2);

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
            await WaitForAttemptAsync(SessionStatus.Reconnecting, attempt);

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
        await WaitForAttemptAsync(SessionStatus.Reconnecting, attempts: 1);

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
        await WaitForAttemptAsync(SessionStatus.Reconnecting, attempts: 1);

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
            await WaitForAttemptAsync(SessionStatus.Reconnecting, attempt);

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
        await WaitForAttemptAsync(SessionStatus.Reconnecting, attempts: 1);

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
    public async Task StopAsync_WhileRunning_StopsAndDisposesTheRunner()
    {
        var runner = await StartRunningAsync();

        await _coordinator.StopAsync(CancellationToken.None);

        Assert.Equal(1, runner.StopCallCount);
        Assert.Equal(1, runner.DisposeCallCount);
        Assert.Equal(SessionStatus.Stopped, _coordinator.Current!.Status);
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

    private Task<SessionId> StartAsync()
        => _coordinator.StartAsync(_profileId, _channelId, InputFile, CancellationToken.None);

    /// <summary>Starts a session and brings it to <see cref="SessionStatus.Running"/> via a first stats line.</summary>
    private async Task<FakeFfmpegProcessRunner> StartRunningAsync()
    {
        await StartAsync();

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

    private Task WaitForAttemptAsync(SessionStatus status, int attempts)
        => WaitUntilAsync(
            () => _coordinator.Current is { } snapshot && snapshot.Status == status && snapshot.ReconnectAttempts == attempts,
            $"the session to reach {status} at attempt {attempts}");

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
    /// Mailbox barrier, for the assertions that must prove that something did NOT happen. The
    /// mailbox has a single reader and preserves order: once a message posted now has been
    /// processed, every message posted before it has been too. Both probes below mutate nothing —
    /// a start is refused outright while a session is active, a stop is a no-op once it is terminal.
    /// </summary>
    private async Task FlushAsync()
    {
        if (_coordinator.HasActiveSession)
            await Assert.ThrowsAsync<DomainException>(
                () => _coordinator.StartAsync(ProfileId.New(), ChannelId.New(), InputFile, CancellationToken.None));
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
