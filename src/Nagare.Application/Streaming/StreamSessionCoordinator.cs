using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;
using Nagare.Domain.Common;
using Nagare.Domain.Sessions;

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
/// Two rules make it work:
/// <list type="number">
/// <item>The loop NEVER awaits the backoff. The delay is scheduled outside and reposts a
/// <c>ReconnectDue</c> message, so a stop is processed immediately even mid-backoff (SPEC §5).</item>
/// <item>Every runner runs under an increasing epoch. Its events carry that epoch, and the
/// loop discards any message whose epoch is stale — a stats line flushed by a process that
/// is already dead can no longer trigger a false recovery.</item>
/// </list>
/// </summary>
public sealed class StreamSessionCoordinator
    : IStreamSessionCoordinator, ISessionMonitor, IHostedService, IAsyncDisposable
{
    private const int LogCapacity = 1000;
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(5);

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
    private CancellationTokenSource? _sessionCts;
    private long _epoch;

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

    public bool HasActiveSession => _session is { Status: not (SessionStatus.Stopped or SessionStatus.Failed) };

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
            if (session is not null)
                FailSession(session, Describe(ex));   // never leave a session Starting without a runner

            await EndSessionAsync();

            if (ex is OperationCanceledException canceled)
                message.Completion.TrySetCanceled(canceled.CancellationToken);
            else
                message.Completion.TrySetException(ex);
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
            _sessionCts?.Cancel();

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
            await EndSessionAsync();
            message.Completion.TrySetException(ex);
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
                DrainEvents(session);

                if (session.Status is SessionStatus.Reconnecting)
                {
                    await CleanupRunnerAsync();
                    ScheduleReconnect(session);
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

        try
        {
            await StartRunnerAsync(command, message.Epoch, cts.Token);
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

        _ = Task.Delay(delay, _time, cts.Token)
            .ContinueWith(
                _ => _mailbox.Writer.TryWrite(new ReconnectDue(epoch)),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
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
        LogAppended?.Invoke(line);
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
    /// it again.
    /// </summary>
    private void FailSession(StreamSession session, string reason)
    {
        switch (session.Status)
        {
            case SessionStatus.Starting:
            case SessionStatus.Reconnecting:
                session.MarkFailed(reason);
                break;

            case SessionStatus.Running:
                // The aggregate only allows Failed after Starting or Reconnecting: take the
                // documented route rather than leaving a live session hanging.
                session.BeginReconnect(reason);
                if (session.Status is SessionStatus.Reconnecting)
                    session.MarkFailed(reason);
                break;

            default:
                return;   // already terminal
        }

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

    private static string Describe(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";

    // ------------------------------------------------------------- projection & logs

    private void DrainEvents(StreamSession session)
    {
        foreach (var evt in session.DomainEvents)
            _logger.LogInformation("Domain event: {Event}", evt.GetType().Name);

        session.ClearDomainEvents();
        NotifyChanged(session);
    }

    private void NotifyChanged(StreamSession session) => Changed?.Invoke(Snapshot(session));

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
            await _loop;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The coordinator loop terminated unexpectedly.");
        }

        // The loop is over: nothing else can touch the session state any more.
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
