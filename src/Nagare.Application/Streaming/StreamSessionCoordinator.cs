using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;
using Nagare.Domain.Common;
using Nagare.Domain.Sessions;

// Aliased, not imported: Nagare.Domain.Channels.Channel would collide with the
// System.Threading.Channels.Channel that backs the mailbox.
using ProtectedStreamKey = Nagare.Domain.Channels.ProtectedStreamKey;

namespace Nagare.Application.Streaming;

/// <summary>
/// Central runtime piece (ARCHITECTURE.md §5). Holds the single active
/// <see cref="StreamSession"/>, builds the ffmpeg command, drives the runner, applies
/// the reconnection backoff, drains domain events and republishes them to the UI via
/// <see cref="ISessionMonitor"/>. Kills the ffmpeg process on application shutdown.
///
/// Concurrency model (ADR-0008): a single sequential loop (mailbox), no lock. Every
/// stimulus — caller, stderr reader thread, process exit callback, backoff timer — becomes
/// a message posted to an unbounded single-reader <see cref="Channel{T}"/>. The loop is the
/// ONLY writer of <see cref="_session"/>, <see cref="_runner"/>, <see cref="_command"/>,
/// <see cref="_lastStats"/> and <see cref="_health"/>, so the aggregate is never mutated
/// concurrently.
///
/// Three rules make it work:
/// <list type="number">
/// <item>The loop NEVER awaits the backoff. The delay is scheduled outside and reposts a
/// <c>ReconnectDue</c> message, so a stop is processed immediately even mid-backoff (SPEC §5).</item>
/// <item>Every runner runs under an increasing epoch. Its events carry that epoch, and the
/// loop discards any message whose epoch is stale — a stats line flushed by a process that
/// is already dead can no longer trigger a false recovery.</item>
/// <item><see cref="StopAsync"/> cancels the session token BEFORE posting its message, and a
/// <c>ReconnectDue</c> that sees that cancellation gives up. This is the one case the epoch cannot
/// catch: a delay that elapses just before the stop is posted queues its message AHEAD of it, still
/// under the current epoch, and would put ffmpeg back on air for the instant it takes to kill it.</item>
/// </list>
/// </summary>
public sealed class StreamSessionCoordinator
    : IStreamSessionCoordinator, ISessionMonitor, IHostedService, IAsyncDisposable
{
    private const int LogCapacity = 1000;
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Logged when barrier 3 aborts a relaunch (a stop was requested while a ReconnectDue was
    /// already in the mailbox). Internal on purpose: the SPEC §5 guard test asserts this exact
    /// message to PROVE the barrier fired — asserting only "no second runner was created" turned
    /// out to pass for the wrong reason under an innocuous-looking reorder of the exit handler.
    /// </summary>
    internal const string StopAbortedReconnectLogMessage = "Reconnection abandoned: a stop has been requested.";

    /// <summary>How long <see cref="DisposeAsync"/> waits for the loop before closing without it.</summary>
    private static readonly TimeSpan LoopShutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly IStreamProfileRepository _profiles;
    private readonly IChannelRepository _channels;
    private readonly IFfmpegCommandBuilder _commandBuilder;
    private readonly IFfmpegProcessRunnerFactory _runnerFactory;
    private readonly ReconnectSettings _reconnectSettings;
    private readonly TimeProvider _time;
    private readonly ILogger<StreamSessionCoordinator> _logger;

    private readonly Channel<CoordinatorMessage> _mailbox = Channel.CreateUnbounded<CoordinatorMessage>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private readonly Task _loop;

    private readonly LinkedList<string> _logs = new();
    private readonly object _logsLock = new();

    // Owned by the mailbox loop: written from there and only from there (ADR-0008).
    private StreamSession? _session;
    private RunnerBinding? _runner;
    private FfmpegCommand? _command;
    private FfmpegStats? _lastStats;
    private HealthIndicator _health = HealthIndicator.Ok;
    private long _epoch;

    /// <summary>
    /// Assigned by the loop only, but CANCELLED from the caller thread too (see <see cref="StopAsync"/>).
    /// A <see cref="CancellationTokenSource"/> is thread-safe and carries no domain state: signalling
    /// it from outside leaves the loop the sole writer of the aggregate (ADR-0008). Volatile so the
    /// caller thread sees the source of the session that is actually running.
    /// </summary>
    private volatile CancellationTokenSource? _sessionCts;

    public StreamSessionCoordinator(
        IStreamProfileRepository profiles,
        IChannelRepository channels,
        IFfmpegCommandBuilder commandBuilder,
        IFfmpegProcessRunnerFactory runnerFactory,
        IOptions<ReconnectSettings> reconnectSettings,
        TimeProvider timeProvider,
        ILogger<StreamSessionCoordinator> logger)
    {
        _profiles = profiles;
        _channels = channels;
        _commandBuilder = commandBuilder;
        _runnerFactory = runnerFactory;
        _reconnectSettings = reconnectSettings.Value;
        _time = timeProvider;
        _logger = logger;

        _loop = Task.Run(RunAsync);
    }

    public bool HasActiveSession => _session is { } session && session.Status.IsActive();

    public event Action<SessionSnapshot>? Changed;
    public event Action<string>? LogAppended;

    public SessionSnapshot? Current
    {
        get
        {
            var session = _session;
            return session is null ? null : Snapshot(session);
        }
    }

    public IReadOnlyList<string> RecentLogs(int maxLines)
    {
        if (maxLines <= 0)
            return [];

        lock (_logsLock)
        {
            var count = Math.Min(maxLines, _logs.Count);
            var skip = _logs.Count - count;
            return [.. _logs.Skip(skip)];
        }
    }

    // ------------------------------------------------------------------ public API

    public async Task<SessionId> StartAsync(ProfileId profileId, ChannelId channelId, string inputFilePath, CancellationToken ct)
    {
        var completion = new TaskCompletionSource<SessionId>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(new StartRequested(profileId, channelId, inputFilePath, ct, completion));

        return await completion.Task.WaitAsync(ct);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        // Cancelled BEFORE the message is posted, and that order is the whole point. If the backoff
        // delay expires just before this call, its ReconnectDue is ALREADY in the mailbox, ahead of
        // the stop (FIFO): the loop would relaunch ffmpeg — a stream briefly back on air on Twitch —
        // only to kill it milliseconds later. Cancelling first makes that queued message give up on
        // its own (see HandleReconnectDueAsync).
        CancelSessionDelays();

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(new StopRequested(ct, completion));

        await completion.Task.WaitAsync(ct);
    }

    private void Post(CoordinatorMessage message)
    {
        // An unbounded writer only refuses once completed, i.e. after DisposeAsync.
        if (!_mailbox.Writer.TryWrite(message))
            throw new ObjectDisposedException(nameof(StreamSessionCoordinator), "The coordinator has been shut down.");
    }

    /// <summary>
    /// Cancels the pending backoff of the current session. Callable from any thread. The source may
    /// be disposed concurrently by the loop ending the session — in which case there is, by
    /// definition, no delay left to cancel.
    /// </summary>
    private void CancelSessionDelays()
    {
        try
        {
            _sessionCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The loop ended the session in between: its backoff died with it.
        }
    }

    // ----------------------------------------------------------------- mailbox loop

    private async Task RunAsync()
    {
        await foreach (var message in _mailbox.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                switch (message)
                {
                    case StartRequested start:
                        await HandleStartAsync(start);
                        break;
                    case StopRequested stop:
                        await HandleStopAsync(stop);
                        break;
                    case StatsReceived stats:
                        HandleStats(stats);
                        break;
                    case ProcessExited exited:
                        await HandleExitedAsync(exited);
                        break;
                    case ReconnectDue due:
                        await HandleReconnectDueAsync(due);
                        break;
                }
            }
            catch (Exception ex)
            {
                // Last resort: each handler deals with its own failures. If one still escapes it
                // is a coordinator bug — fail LOUDLY (ADR-0008), never leave a caller hanging,
                // and never let the loop die.
                _logger.LogError(ex, "Unhandled error while processing {Message}. Tearing the session down.", message.GetType().Name);
                CompleteWithError(message, ex);
                await TearDownAfterFailureAsync(ex);
            }
        }
    }

    private async Task HandleStartAsync(StartRequested message)
    {
        if (HasActiveSession)
        {
            // Refused BEFORE touching any state: the running session must survive intact.
            message.Completion.TrySetException(
                new DomainException("A session is already active. Stop it before starting a new one."));
            return;
        }

        StreamSession? session = null;
        try
        {
            message.Ct.ThrowIfCancellationRequested();

            var profile = await _profiles.GetByIdAsync(message.ProfileId, message.Ct)
                ?? throw new DomainException($"Profile {message.ProfileId} not found.");
            var channel = await _channels.GetByIdAsync(message.ChannelId, message.Ct)
                ?? throw new DomainException($"Channel {message.ChannelId} not found.");

            var command = _commandBuilder.Build(profile, channel, message.InputFilePath);
            session = StreamSession.Launch(message.ProfileId, message.ChannelId, message.InputFilePath, _reconnectSettings.ToPolicy());

            ClearLogs();                       // a new session never shows the previous one's lines
            _lastStats = null;
            _health = HealthIndicator.Ok;
            _command = command;
            _sessionCts = new CancellationTokenSource();
            _session = session;

            var epoch = ++_epoch;
            DrainEvents(session);

            await StartRunnerAsync(command, epoch, message.Ct);

            message.Completion.TrySetResult(session.Id);
        }
        catch (Exception ex)
        {
            // Scrub BEFORE tearing the session down: EndSessionAsync clears _command, and with it the
            // secrets the scrubber needs. Doing it after would hand the caller — and the screen — the
            // raw text, key included. (Caught by the test, not by reading the code.)
            var surfaced = Surface(ex);

            if (session is not null)
                FailSession(session, Describe(ex));   // never leave a session Starting without a runner

            await EndSessionAsync();

            if (ex is OperationCanceledException canceled)
                message.Completion.TrySetCanceled(canceled.CancellationToken);
            else
                message.Completion.TrySetException(surfaced);
        }
    }

    private async Task HandleStopAsync(StopRequested message)
    {
        var session = _session;
        if (session is null || session.Status is SessionStatus.Stopped or SessionStatus.Failed)
        {
            message.Completion.TrySetResult();
            return;
        }

        try
        {
            // Barrier 1: cancels the pending backoff delay, so its ReconnectDue is never posted.
            // Already done by StopAsync before posting (Cancel is idempotent); repeated here so a
            // stop message alone carries the full contract, whoever posts it.
            CancelSessionDelays();

            // Barrier 2: every message already in flight (including a ReconnectDue that just
            // won the race) becomes stale. No ffmpeg can be relaunched after a stop.
            _epoch++;

            var runner = _runner?.Runner;
            if (runner is not null)
            {
                try
                {
                    await runner.StopAsync(GracePeriod, message.Ct);
                }
                catch (Exception ex)
                {
                    // The process is killed anyway when the runner is disposed below.
                    _logger.LogWarning(ex, "Error while stopping the ffmpeg runner.");
                }
            }

            session.Stop();
            DrainEvents(session);
            await EndSessionAsync();

            message.Completion.TrySetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping the session.");

            var surfaced = Surface(ex);   // before EndSessionAsync clears the secrets (see HandleStartAsync)
            await EndSessionAsync();
            message.Completion.TrySetException(surfaced);
        }
    }

    private void HandleStats(StatsReceived message)
    {
        if (IsStale(message.Epoch))
            return;

        var session = _session;
        if (session is null)
            return;

        _health = HealthOf(_lastStats, message.Stats);
        _lastStats = message.Stats;

        // First stats after Starting/Reconnecting -> Running.
        if (session.Status is SessionStatus.Starting or SessionStatus.Reconnecting)
        {
            session.MarkRunning();
            DrainEvents(session);
        }
        else
        {
            NotifyChanged(session);
        }
    }

    private async Task HandleExitedAsync(ProcessExited message)
    {
        if (IsStale(message.Epoch))
            return;

        var session = _session;
        if (session is null)
            return;

        // The runner is dead: the stderr readers may still flush buffered lines, which belong
        // to a past generation from now on (ADR-0008, defect c).
        _epoch++;

        var reason = $"ffmpeg exited unexpectedly (code {message.ExitCode}).";

        switch (session.Status)
        {
            case SessionStatus.Starting:
                // Initial failure -> straight to Failed, no backoff (design rule, §2.4).
                session.MarkFailed(reason);
                DrainEvents(session);
                await EndSessionAsync();
                break;

            case SessionStatus.Running:
            case SessionStatus.Reconnecting:
                // From Running a drop was detected; from Reconnecting a relaunch died before
                // producing stats, so the aggregate counts a further attempt.
                session.BeginReconnect(reason);

                if (session.Status is SessionStatus.Reconnecting)
                {
                    // Forget the dead process's stats. HealthOf compares dropped-frame counters
                    // between two samples; the relaunched ffmpeg restarts its own counters at zero,
                    // so keeping the corpse's figures would make the first real burst of drops read
                    // as an improvement — a Warning silently downgraded to Ok.
                    _lastStats = null;
                    _health = HealthIndicator.Ok;
                }

                DrainEvents(session);

                if (session.Status is SessionStatus.Reconnecting)
                {
                    // The backoff is armed BEFORE the dead process is reaped: killing it can block
                    // for a while, and that time must not eat into the reconnection window. Nothing
                    // can jump the gun — the loop is sequential, so the ReconnectDue it may post
                    // meanwhile simply waits in the mailbox until this handler returns.
                    ScheduleReconnect(session);
                    await CleanupRunnerAsync();
                }
                else
                {
                    await EndSessionAsync();   // attempts exhausted -> Failed
                }

                break;

            default:
                break;   // already terminal: the exit was expected
        }
    }

    private async Task HandleReconnectDueAsync(ReconnectDue message)
    {
        if (IsStale(message.Epoch))
            return;

        var session = _session;
        var command = _command;
        var cts = _sessionCts;

        if (session is null || command is null || cts is null || session.Status is not SessionStatus.Reconnecting)
            return;

        if (cts.IsCancellationRequested)
        {
            // Barrier 3. The epoch cannot catch this one: the delay expired just BEFORE the stop was
            // posted, so this message is ahead of it in the FIFO and still carries the current epoch.
            // A stop is on its way — putting ffmpeg back on air now, to kill it milliseconds later,
            // is exactly the ghost broadcast we refuse.
            _logger.LogInformation(StopAbortedReconnectLogMessage);
            return;
        }

        try
        {
            await StartRunnerAsync(command, message.Epoch, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // The stop landed while the runner was starting. Not a failure: the pending StopRequested
            // owns the ending of this session, and disposes the half-started runner with it.
            _logger.LogInformation("Reconnection interrupted by a stop request.");
        }
        catch (Exception ex)
        {
            // Typically a Win32Exception: the ffmpeg binary is missing, moved or locked by an
            // antivirus. Without this the session would stay Reconnecting forever, with no runner
            // left to ever raise Exited again: a zombie (ADR-0008, defect b).
            _logger.LogError(ex, "Failed to relaunch ffmpeg; the session is marked as failed.");
            FailSession(session, Describe(ex));
            await EndSessionAsync();
        }
    }

    /// <summary>
    /// Plans the backoff OUTSIDE the loop: awaiting it inside would make the loop deaf to a
    /// stop request for up to the whole backoff window, which is precisely the defect this
    /// design removes (ADR-0008). The delay simply reposts a message.
    /// </summary>
    private void ScheduleReconnect(StreamSession session)
    {
        var cts = _sessionCts;
        if (cts is null || _command is null)
        {
            _logger.LogError("Cannot schedule a reconnection without an active session command.");
            return;
        }

        var epoch = _epoch;
        var delay = session.Policy.DelayFor(session.ReconnectAttempts);

        _logger.LogInformation(
            "Reconnection attempt {Attempt}/{MaxAttempts} scheduled in {Delay}.",
            session.ReconnectAttempts, session.Policy.MaxAttempts, delay);

        // ExecuteSynchronously: the continuation only drops a message into an unbounded channel that
        // never dispatches its reader inline (AllowSynchronousContinuations = false). Running it on
        // the timer thread costs nothing, spares a thread-pool hop, and makes the post happen at a
        // defined instant — the moment the delay elapses — instead of "some time after".
        _ = Task.Delay(delay, _time, cts.Token)
            .ContinueWith(
                _ => _mailbox.Writer.TryWrite(new ReconnectDue(epoch)),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    // -------------------------------------------------------------- runner plumbing

    private async Task StartRunnerAsync(FfmpegCommand command, long epoch, CancellationToken ct)
    {
        var runner = _runnerFactory.Create();

        // The runner raises its events from background threads: they are tagged with the epoch
        // of the process that emits them and handed over to the loop.
        Action<string> onOutputLine = OnOutputLine;
        Action<FfmpegStats> onStats = stats => _mailbox.Writer.TryWrite(new StatsReceived(epoch, stats));
        Action<int> onExited = exitCode => _mailbox.Writer.TryWrite(new ProcessExited(epoch, exitCode));

        runner.OutputLineReceived += onOutputLine;
        runner.StatsReceived += onStats;
        runner.Exited += onExited;

        // Assigned BEFORE the start: if it throws, the caller still disposes the runner.
        _runner = new RunnerBinding(runner, onOutputLine, onStats, onExited);

        await runner.StartAsync(command, ct);
    }

    /// <summary>Log lines never touch the aggregate: they keep their own buffer and stay off the mailbox.</summary>
    private void OnOutputLine(string line)
    {
        Append(line);
        RaiseSafely(() => LogAppended?.Invoke(line), nameof(LogAppended));
    }

    private async Task CleanupRunnerAsync()
    {
        var binding = _runner;
        if (binding is null)
            return;

        _runner = null;

        binding.Runner.OutputLineReceived -= binding.OnOutputLine;
        binding.Runner.StatsReceived -= binding.OnStats;
        binding.Runner.Exited -= binding.OnExited;

        try
        {
            await binding.Runner.DisposeAsync();   // kills the process if it is still alive
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while disposing the ffmpeg runner.");
        }
    }

    /// <summary>
    /// Ends the session lifecycle: invalidates the epoch, disposes the runner AND the session
    /// CTS — which used to leak, being overwritten at the next start instead of disposed. The
    /// session itself is kept in its terminal state so the UI can still display it.
    /// </summary>
    private async Task EndSessionAsync()
    {
        _epoch++;

        await CleanupRunnerAsync();

        _sessionCts?.Dispose();
        _sessionCts = null;
        _command = null;
    }

    private async Task TearDownAfterFailureAsync(Exception error)
    {
        try
        {
            var session = _session;
            if (session is not null)
                FailSession(session, Describe(error));

            await EndSessionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while tearing the session down.");
        }
    }

    /// <summary>
    /// Drives the aggregate to <see cref="SessionStatus.Failed"/> from wherever it is. A session
    /// left in a non-terminal state without a runner would be a zombie: nothing would ever move
    /// it again. Reachable from Running only on a coordinator fault — the aggregate accepts it,
    /// and <see cref="SessionFailed"/> then says exactly what happened.
    /// </summary>
    private void FailSession(StreamSession session, string reason)
    {
        if (session.Status is SessionStatus.Stopped or SessionStatus.Failed)
            return;   // already terminal

        session.MarkFailed(reason);
        DrainEvents(session);
    }

    private bool IsStale(long epoch)
    {
        if (epoch == _epoch)
            return false;

        _logger.LogDebug("Message from a stale epoch ignored ({Epoch} != {CurrentEpoch}).", epoch, _epoch);
        return true;
    }

    private static void CompleteWithError(CoordinatorMessage message, Exception error)
    {
        switch (message)
        {
            case StartRequested start:
                start.Completion.TrySetException(error);
                break;
            case StopRequested stop:
                stop.Completion.TrySetException(error);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Turns an exception into the reason carried by <see cref="SessionFailed"/> and
    /// <see cref="StreamSession.LastError"/> — both surfaced in the UI.
    ///
    /// The reason is SCRUBBED. `StreamSession` documents that the coordinator hands it an already
    /// scrubbed text, and ARCHITECTURE §6.3 makes the same promise. No exception reachable here
    /// carries the ffmpeg arguments today, so nothing leaks — but the invariant was merely true by
    /// luck, and a future exception type quoting the command would have turned that luck into a
    /// stream key on screen. The secrets are right here; honouring the contract costs one call.
    /// </summary>
    private string Describe(Exception ex) => Scrub($"{ex.GetType().Name}: {ex.Message}");

    /// <summary>
    /// Prepares an exception to cross the boundary to the caller — and therefore to the SCREEN, where
    /// the view model shows its Message in an InfoBar.
    ///
    /// <see cref="DomainException"/> passes through untouched: its message is ours, it holds no
    /// secret, and the UI relies on its type to tell a validation error from a failure. Anything else
    /// is an infrastructure exception whose text we do not control — it is re-surfaced SCRUBBED.
    /// Today none of them embeds the ffmpeg arguments, but that is luck, not a guarantee, and this is
    /// the one path that leads straight to the user's eyes.
    /// </summary>
    private Exception Surface(Exception ex)
        => ex is DomainException ? ex : new StreamOperationException(Describe(ex));

    /// <summary>Replaces every secret of the active command by the mask, longest first.</summary>
    private string Scrub(string text)
    {
        var secrets = _command?.Secrets;
        if (secrets is null || secrets.Count == 0)
            return text;

        // Longest first: a secret containing another one must be masked as a whole.
        foreach (var secret in secrets.Where(s => !string.IsNullOrEmpty(s)).OrderByDescending(s => s.Length))
            text = text.Replace(secret, ProtectedStreamKey.Mask, StringComparison.Ordinal);

        return text;
    }

    /// <summary>
    /// Raises a subscriber callback WITHOUT ever letting it fail the session. Subscribers run on
    /// the mailbox loop thread: a WinUI view model throwing while marshalling to the dispatcher
    /// would otherwise bubble into the loop's catch and kill a LIVE broadcast. A broken UI must
    /// never take the stream down with it.
    /// </summary>
    private void RaiseSafely(Action raise, string what)
    {
        try
        {
            raise();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A {What} subscriber threw. The session is unaffected.", what);
        }
    }

    // ------------------------------------------------------------- projection & logs

    private void DrainEvents(StreamSession session)
    {
        foreach (var evt in session.DomainEvents)
            _logger.LogInformation("Domain event: {Event}", evt.GetType().Name);

        session.ClearDomainEvents();
        NotifyChanged(session);
    }

    private void NotifyChanged(StreamSession session)
    {
        var snapshot = Snapshot(session);
        RaiseSafely(() => Changed?.Invoke(snapshot), nameof(Changed));
    }

    private SessionSnapshot Snapshot(StreamSession session)
        => new(
            session.Id,
            session.Status,
            _lastStats,
            _health,
            session.ReconnectAttempts,
            session.LastError);

    /// <summary>
    /// Warning when ffmpeg falls behind real time OR when the drop counter grows since the
    /// previous sample (SPEC §6 — the growing drops were announced but never taken into account).
    /// </summary>
    private static HealthIndicator HealthOf(FfmpegStats? previous, FfmpegStats current)
        => current.Speed < 1.0 || current.DroppedFrames > (previous?.DroppedFrames ?? 0)
            ? HealthIndicator.Warning
            : HealthIndicator.Ok;

    private void Append(string line)
    {
        lock (_logsLock)
        {
            _logs.AddLast(line);
            while (_logs.Count > LogCapacity)
                _logs.RemoveFirst();
        }
    }

    private void ClearLogs()
    {
        lock (_logsLock)
            _logs.Clear();
    }

    // ---------------------------------------------------------------- host lifecycle

    // IHostedService: nothing to start eagerly; ensures the singleton is created so it
    // participates in graceful shutdown (kill of the ffmpeg process). Explicit
    // implementation avoids the signature clash with IStreamSessionCoordinator.StopAsync.
    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    async Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        if (HasActiveSession)
            await StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _mailbox.Writer.TryComplete();   // the loop drains what is left, then ends

        try
        {
            await _loop.WaitAsync(LoopShutdownTimeout, _time);
        }
        catch (TimeoutException)
        {
            // Safety belt, not an expected path: the loop ends by construction once the mailbox is
            // completed. But SPEC §5 demands that ffmpeg be killed when the application closes, and
            // a loop stuck on a hung handler would do the exact opposite — freeze the shutdown while
            // ffmpeg keeps publishing. We give up waiting and kill it anyway; the cleanup below then
            // races with a loop that should no longer exist, and that is the lesser evil.
            _logger.LogWarning(
                "The coordinator loop did not end within {Timeout}; shutting down anyway.",
                LoopShutdownTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The coordinator loop terminated unexpectedly.");
        }

        await CleanupRunnerAsync();

        _sessionCts?.Dispose();
        _sessionCts = null;
    }

    // -------------------------------------------------------------------- mailbox DTOs

    private abstract record CoordinatorMessage;

    private sealed record StartRequested(
        ProfileId ProfileId,
        ChannelId ChannelId,
        string InputFilePath,
        CancellationToken Ct,
        TaskCompletionSource<SessionId> Completion) : CoordinatorMessage;

    private sealed record StopRequested(
        CancellationToken Ct,
        TaskCompletionSource Completion) : CoordinatorMessage;

    private sealed record StatsReceived(long Epoch, FfmpegStats Stats) : CoordinatorMessage;

    private sealed record ProcessExited(long Epoch, int ExitCode) : CoordinatorMessage;

    private sealed record ReconnectDue(long Epoch) : CoordinatorMessage;

    /// <summary>A started runner with the exact delegates it was subscribed with, so they can be removed.</summary>
    private sealed record RunnerBinding(
        IFfmpegProcessRunner Runner,
        Action<string> OnOutputLine,
        Action<FfmpegStats> OnStats,
        Action<int> OnExited);
}
