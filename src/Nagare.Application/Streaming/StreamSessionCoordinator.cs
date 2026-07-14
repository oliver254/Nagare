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
    private readonly ILogger<StreamSessionCoordinator> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly LinkedList<string> _logs = new();
    private readonly object _logsLock = new();

    private StreamSession? _session;
    private IFfmpegProcessRunner? _runner;
    private FfmpegCommand? _command;
    private FfmpegStats? _lastStats;
    private CancellationTokenSource? _sessionCts;

    public StreamSessionCoordinator(
        IStreamProfileRepository profiles,
        IChannelRepository channels,
        IFfmpegCommandBuilder commandBuilder,
        IFfmpegProcessRunnerFactory runnerFactory,
        IOptions<ReconnectSettings> reconnectSettings,
        ILogger<StreamSessionCoordinator> logger)
    {
        _profiles = profiles;
        _channels = channels;
        _commandBuilder = commandBuilder;
        _runnerFactory = runnerFactory;
        _reconnectSettings = reconnectSettings.Value;
        _logger = logger;
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

    public async Task<SessionId> StartAsync(ProfileId profileId, ChannelId channelId, string inputFilePath, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (HasActiveSession)
                throw new DomainException("A session is already active. Stop it before starting a new one.");

            var profile = await _profiles.GetByIdAsync(profileId, ct)
                ?? throw new DomainException($"Profile {profileId} not found.");
            var channel = await _channels.GetByIdAsync(channelId, ct)
                ?? throw new DomainException($"Channel {channelId} not found.");

            _command = _commandBuilder.Build(profile, channel, inputFilePath);
            _lastStats = null;
            _sessionCts = new CancellationTokenSource();

            var session = StreamSession.Launch(profileId, channelId, inputFilePath, _reconnectSettings.ToPolicy());
            _session = session;
            DrainEvents(session);

            await StartRunnerAsync(_command, ct);
            return session.Id;
        }
        catch
        {
            await CleanupAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var session = _session;
            if (session is null || session.Status is SessionStatus.Stopped or SessionStatus.Failed)
                return;

            _sessionCts?.Cancel();

            var runner = _runner;
            if (runner is not null)
            {
                try
                {
                    await runner.StopAsync(GracePeriod, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while stopping the ffmpeg runner.");
                }
            }

            session.Stop();
            DrainEvents(session);
            await CleanupRunnerAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StartRunnerAsync(FfmpegCommand command, CancellationToken ct)
    {
        var runner = _runnerFactory.Create();
        _runner = runner;

        runner.OutputLineReceived += OnOutputLine;
        runner.StatsReceived += OnStatsReceived;
        runner.Exited += OnExited;

        await runner.StartAsync(command, ct);
    }

    private void OnOutputLine(string line)
    {
        Append(line);
        LogAppended?.Invoke(line);
    }

    private void OnStatsReceived(FfmpegStats stats)
    {
        _lastStats = stats;

        var session = _session;
        if (session is null)
            return;

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

    private void OnExited(int exitCode)
    {
        // Fire-and-forget: the runner raises Exited from a background reader thread.
        _ = HandleExitAsync(exitCode);
    }

    private async Task HandleExitAsync(int exitCode)
    {
        await _gate.WaitAsync();
        try
        {
            var session = _session;
            if (session is null)
                return;

            // Expected exit after a user stop: nothing to do.
            if (session.Status is SessionStatus.Stopped or SessionStatus.Failed)
                return;

            var reason = $"ffmpeg exited unexpectedly (code {exitCode}).";

            switch (session.Status)
            {
                case SessionStatus.Starting:
                    // Initial failure -> straight to Failed, no backoff (design rule).
                    session.MarkFailed(reason);
                    DrainEvents(session);
                    await CleanupRunnerAsync();
                    break;

                case SessionStatus.Running:
                    session.BeginReconnect(reason);
                    DrainEvents(session);

                    if (session.Status == SessionStatus.Reconnecting)
                        await ScheduleReconnectAsync(session);
                    else
                        await CleanupRunnerAsync();   // attempts exhausted -> Failed
                    break;

                case SessionStatus.Reconnecting:
                    // A relaunch attempt failed before producing stats: count another attempt.
                    session.BeginReconnect(reason);
                    DrainEvents(session);

                    if (session.Status == SessionStatus.Reconnecting)
                        await ScheduleReconnectAsync(session);
                    else
                        await CleanupRunnerAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling ffmpeg exit.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ScheduleReconnectAsync(StreamSession session)
    {
        var cts = _sessionCts;
        var command = _command;
        if (cts is null || command is null)
            return;

        var delay = session.Policy.DelayFor(session.ReconnectAttempts);

        await CleanupRunnerAsync();

        try
        {
            await Task.Delay(delay, cts.Token);
            await StartRunnerAsync(command, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Session stopped during the backoff window: nothing to relaunch.
        }
    }

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
            HealthOf(_lastStats),
            session.ReconnectAttempts,
            session.LastError);

    private static HealthIndicator HealthOf(FfmpegStats? stats)
        => stats is { Speed: < 1.0 } ? HealthIndicator.Warning : HealthIndicator.Ok;

    private void Append(string line)
    {
        lock (_logsLock)
        {
            _logs.AddLast(line);
            while (_logs.Count > LogCapacity)
                _logs.RemoveFirst();
        }
    }

    private async Task CleanupRunnerAsync()
    {
        var runner = _runner;
        if (runner is null)
            return;

        runner.OutputLineReceived -= OnOutputLine;
        runner.StatsReceived -= OnStatsReceived;
        runner.Exited -= OnExited;
        _runner = null;

        try
        {
            await runner.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while disposing the ffmpeg runner.");
        }
    }

    private async Task CleanupAsync()
    {
        await CleanupRunnerAsync();
        _sessionCts?.Dispose();
        _sessionCts = null;
        _command = null;
        _session = null;
    }

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
        await CleanupRunnerAsync();
        _sessionCts?.Dispose();
        _gate.Dispose();
    }
}
