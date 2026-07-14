using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Monbsoft.BrilliantMediator.Abstractions;
using Nagare.Application.Abstractions;
using Nagare.Application.Channels;
using Nagare.Application.Media;
using Nagare.Application.Profiles;
using Nagare.Application.Streaming;
using Nagare.Domain.Common;
using Nagare.Domain.Profiles;
using Nagare.Domain.Sessions;
using Nagare.Presentation.Abstractions;

namespace Nagare.Presentation.ViewModels;

/// <summary>
/// The broadcast page: choose file / profile / channel, preview the ffmpeg command (key masked —
/// SPEC §4), start, stop, and watch the session live (plan §5).
///
/// THREADING — the whole point of this class. <see cref="ISessionMonitor"/> raises
/// <c>Changed</c> from the coordinator's mailbox loop and <c>LogAppended</c> from the ffmpeg stderr
/// reader thread. NOTHING in here touches an observable property outside
/// <see cref="IUiDispatcher.Post"/>, and the two flows are damped before they reach the UI:
///
/// <list type="number">
/// <item><b>Logs</b>: every ffmpeg line (progress lines included — the runner emits them all) is
/// queued and drained by a SINGLE pending UI callback, into a ring buffer bounded to
/// <see cref="MaxLogLines"/>. Unbounded growth is a leak; one dispatcher callback per line under a
/// stderr burst is a frozen UI.</item>
/// <item><b>Stats</b>: throttled to one UI update per <see cref="StatsThrottle"/>. ffmpeg emits
/// several stats lines per second; forwarding each one is the surest way to kill the UI. Status
/// changes bypass the throttle — they are rare and must be instant.</item>
/// </list>
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase, IDisposable
{
    /// <summary>Ring buffer bound, and the number of lines rehydrated when the page comes back.</summary>
    public const int MaxLogLines = 500;

    /// <summary>One stats refresh per second — see the class remarks.</summary>
    public static readonly TimeSpan StatsThrottle = TimeSpan.FromSeconds(1);

    private readonly IMediator _mediator;
    private readonly ISessionMonitor _monitor;
    private readonly IUiDispatcher _dispatcher;
    private readonly IVideoFilePicker _filePicker;
    private readonly TimeProvider _time;

    /// <summary>Lines waiting for the UI thread. Written from the stderr reader thread, drained on the UI thread.</summary>
    private readonly ConcurrentQueue<string> _incomingLogs = new();

    /// <summary>0/1 via Interlocked: is a drain callback already on its way to the UI thread?</summary>
    private int _logDrainScheduled;

    private FfmpegEnvironmentReport? _environment;
    private bool _subscribed;

    // Throttle state. Touched ONLY from OnSessionChanged, which the coordinator calls from its
    // single mailbox loop thread: no lock needed, and none would help.
    private SessionSnapshot? _lastObserved;
    private DateTimeOffset _lastPublishedAt;

    public DashboardViewModel(
        IMediator mediator,
        ISessionMonitor monitor,
        IUiDispatcher dispatcher,
        IVideoFilePicker filePicker,
        TimeProvider time)
    {
        _mediator = mediator;
        _monitor = monitor;
        _dispatcher = dispatcher;
        _filePicker = filePicker;
        _time = time;
    }

    public ObservableCollection<StreamProfileDto> Profiles { get; } = [];
    public ObservableCollection<ChannelDto> Channels { get; } = [];

    /// <summary>Bounded ring buffer — see the class remarks. Only ever touched on the UI thread.</summary>
    public ObservableCollection<string> Logs { get; } = [];

    // ------------------------------------------------------------------ selection

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private StreamProfileDto? _selectedProfile;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private ChannelDto? _selectedChannel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string? _inputFilePath;

    /// <summary>ffprobe report of the chosen file: duration, resolution, codecs.</summary>
    [ObservableProperty]
    private string? _mediaSummary;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string? _mediaError;

    /// <summary>Generated ffmpeg command line, KEY MASKED (SPEC §4). Comes from the domain, never rebuilt here.</summary>
    [ObservableProperty]
    private string? _commandPreview;

    /// <summary>Blocking issue (ffmpeg missing, NVENC required but absent). Null = nothing blocks.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string? _environmentIssue;

    // --------------------------------------------------------------- live session

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand))]
    private bool _isSessionActive;

    [ObservableProperty]
    private string _statusLabel = "Aucune session";

    [ObservableProperty]
    private SessionStatus? _status;

    [ObservableProperty]
    private double _fps;

    [ObservableProperty]
    private double _bitrateKbps;

    [ObservableProperty]
    private double _speed;

    [ObservableProperty]
    private int _droppedFrames;

    [ObservableProperty]
    private int _reconnectAttempts;

    /// <summary>speed &lt; 1.0x or growing drops (SPEC §6). Rendered as a distinct colour.</summary>
    [ObservableProperty]
    private bool _isHealthWarning;

    [ObservableProperty]
    private string? _lastError;

    // -------------------------------------------------------------------- commands

    /// <summary>
    /// Page load: environment check, lists, and — if a session is already running — rehydration
    /// from <see cref="ISessionMonitor"/>. Rehydration is why the subscription can be dropped when
    /// the page goes away without losing anything: the coordinator keeps the truth.
    /// </summary>
    [RelayCommand]
    private Task LoadAsync() => RunGuardedAsync(async () =>
    {
        _environment = await _mediator.SendAsync<GetFfmpegEnvironmentQuery, FfmpegEnvironmentReport>(
            new GetFfmpegEnvironmentQuery());
        RefreshEnvironmentIssue();

        var profiles = await _mediator.SendAsync<GetStreamProfilesQuery, IReadOnlyList<StreamProfileDto>>(
            new GetStreamProfilesQuery());
        Replace(Profiles, profiles);

        var channels = await _mediator.SendAsync<GetChannelsQuery, IReadOnlyList<ChannelDto>>(
            new GetChannelsQuery());
        Replace(Channels, channels);

        Subscribe();

        Logs.Clear();
        foreach (var line in _monitor.RecentLogs(MaxLogLines))
            Logs.Add(line);

        if (_monitor.Current is { } current)
            ApplySnapshot(current);
    });

    [RelayCommand]
    private Task PickFileAsync() => RunGuardedAsync(async () =>
    {
        var path = await _filePicker.PickAsync();
        if (path is null)
            return;   // cancelled

        InputFilePath = path;
        await ValidateFileAsync();
        await RefreshPreviewAsync();
    });

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartAsync() => RunGuardedAsync(async () =>
    {
        // A new session shows its own lines only — the coordinator clears its buffer too.
        Logs.Clear();
        _incomingLogs.Clear();

        await _mediator.DispatchAsync<StartStreamCommand, SessionId>(
            new StartStreamCommand(SelectedProfile!.Id, SelectedChannel!.Id, InputFilePath!));
    });

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync() => RunGuardedAsync(() => _mediator.DispatchAsync(new StopStreamCommand()));

    private bool CanStart()
        => !IsSessionActive
           && EnvironmentIssue is null
           && SelectedProfile is not null
           && SelectedChannel is not null
           && !string.IsNullOrWhiteSpace(InputFilePath)
           && MediaError is null;

    private bool CanStop() => IsSessionActive;

    // ------------------------------------------------------------ selection effects

    partial void OnSelectedProfileChanged(StreamProfileDto? value)
    {
        RefreshEnvironmentIssue();   // an NVENC profile on a machine without NVENC blocks the start
        QueuePreviewRefresh();
    }

    partial void OnSelectedChannelChanged(ChannelDto? value) => QueuePreviewRefresh();

    /// <summary>
    /// Fire-and-forget refresh triggered by a selection change. Safe: the body cannot throw — it is
    /// wrapped — and it runs on the UI thread, where the setter fired.
    /// </summary>
    private void QueuePreviewRefresh() => _ = RefreshPreviewAsync();

    private async Task ValidateFileAsync()
    {
        var result = await _mediator.SendAsync<ValidateMediaFileQuery, MediaValidationResult>(
            new ValidateMediaFileQuery(InputFilePath!));

        if (!result.Exists || !result.Readable || result.Error is not null)
        {
            MediaError = result.Error
                ?? (result.Exists ? "Fichier illisible par ffprobe." : "Fichier introuvable.");
            MediaSummary = null;
            return;
        }

        MediaError = null;
        MediaSummary = string.Join(" · ", new[]
        {
            result.Duration is { } duration ? duration.ToString(@"hh\:mm\:ss") : null,
            result.Width is { } width && result.Height is { } height ? $"{width}×{height}" : null,
            result.Fps is { } fps ? $"{fps:0.##} fps" : null,
            result.VideoCodec,
            result.AudioCodec
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    /// <summary>
    /// Asks the domain for the command line to show. The key NEVER travels here: the query answers
    /// <c>FfmpegCommand.MaskedCommandLine</c>, and nothing in this ViewModel could unmask it.
    /// </summary>
    private async Task RefreshPreviewAsync()
    {
        if (SelectedProfile is null || SelectedChannel is null || string.IsNullOrWhiteSpace(InputFilePath) || MediaError is not null)
        {
            CommandPreview = null;
            return;
        }

        try
        {
            CommandPreview = await _mediator.SendAsync<BuildCommandPreviewQuery, string>(
                new BuildCommandPreviewQuery(SelectedProfile.Id, SelectedChannel.Id, InputFilePath));
        }
        catch (Exception ex)
        {
            CommandPreview = null;
            ErrorMessage = ex.Message;
        }
    }

    private void RefreshEnvironmentIssue()
    {
        if (_environment is null)
        {
            EnvironmentIssue = null;
            return;
        }

        if (!_environment.FfmpegAvailable)
        {
            EnvironmentIssue = _environment.Error
                ?? "ffmpeg est introuvable. Renseignez « Nagare:Ffmpeg:ExecutablePath » ou ajoutez ffmpeg au PATH.";
            return;
        }

        if (!_environment.FfprobeAvailable)
        {
            EnvironmentIssue = "ffprobe est introuvable : la validation des fichiers vidéo est impossible.";
            return;
        }

        if (SelectedProfile is { Video.Codec: VideoCodec.H264Nvenc or VideoCodec.HevcNvenc } && !_environment.NvencAvailable)
        {
            EnvironmentIssue = "Le profil sélectionné exige NVENC, indisponible sur cette machine. "
                + "Choisissez un profil libx264.";
            return;
        }

        EnvironmentIssue = null;
    }

    // ------------------------------------------------------- real time (phase 5)

    private void Subscribe()
    {
        if (_subscribed)
            return;

        _monitor.Changed += OnSessionChanged;
        _monitor.LogAppended += OnLogAppended;
        _subscribed = true;
    }

    /// <summary>
    /// Called from the coordinator's MAILBOX LOOP thread. Decides what deserves to reach the UI,
    /// and marshals it — nothing else.
    ///
    /// Status, reconnection count and last error go through IMMEDIATELY: they change rarely and the
    /// user must see a drop the instant it happens. Stats-only snapshots are THROTTLED: ffmpeg emits
    /// them several times per second. The health indicator rides with the stats (it is computed from
    /// them, and a speed hovering around 1.0x would otherwise flip-flop past the throttle and defeat
    /// it) — at most one second late, which is what a health LED can afford.
    /// </summary>
    private void OnSessionChanged(SessionSnapshot snapshot)
    {
        var previous = _lastObserved;
        _lastObserved = snapshot;

        var statsOnly = previous is not null
            && previous.Id == snapshot.Id
            && previous.Status == snapshot.Status
            && previous.ReconnectAttempts == snapshot.ReconnectAttempts
            && previous.LastError == snapshot.LastError;

        var now = _time.GetUtcNow();
        if (statsOnly && now - _lastPublishedAt < StatsThrottle)
            return;   // dropped on purpose: the next line is a fraction of a second away

        _lastPublishedAt = now;
        _dispatcher.Post(() => ApplySnapshot(snapshot));
    }

    /// <summary>UI thread only.</summary>
    private void ApplySnapshot(SessionSnapshot snapshot)
    {
        Status = snapshot.Status;
        StatusLabel = LabelOf(snapshot.Status);
        IsSessionActive = snapshot.Status is SessionStatus.Starting or SessionStatus.Running or SessionStatus.Reconnecting;
        ReconnectAttempts = snapshot.ReconnectAttempts;
        LastError = snapshot.LastError;
        IsHealthWarning = snapshot.Health == HealthIndicator.Warning;

        Fps = snapshot.Stats?.Fps ?? 0;
        BitrateKbps = snapshot.Stats?.BitrateKbps ?? 0;
        Speed = snapshot.Stats?.Speed ?? 0;
        DroppedFrames = snapshot.Stats?.DroppedFrames ?? 0;
    }

    /// <summary>
    /// Called from the ffmpeg STDERR READER thread, for every single line (the runner forwards the
    /// progress lines too). One dispatcher callback per line would flood the UI thread under a
    /// burst; instead the line is queued and a SINGLE drain is scheduled until it runs.
    /// </summary>
    private void OnLogAppended(string line)
    {
        _incomingLogs.Enqueue(line);

        if (Interlocked.CompareExchange(ref _logDrainScheduled, 1, 0) != 0)
            return;   // a drain is already queued: it will pick this line up

        if (!_dispatcher.Post(DrainLogs))
            Interlocked.Exchange(ref _logDrainScheduled, 0);   // UI gone; let a later line try again
    }

    /// <summary>UI thread only. The flag is released FIRST, so lines arriving mid-drain schedule the next one.</summary>
    private void DrainLogs()
    {
        Interlocked.Exchange(ref _logDrainScheduled, 0);

        while (_incomingLogs.TryDequeue(out var line))
        {
            Logs.Add(line);

            while (Logs.Count > MaxLogLines)
                Logs.RemoveAt(0);   // ring buffer: an unbounded log view is a memory leak
        }
    }

    private static string LabelOf(SessionStatus status) => status switch
    {
        SessionStatus.Starting => "Démarrage",
        SessionStatus.Running => "En cours",
        SessionStatus.Reconnecting => "Reconnexion",
        SessionStatus.Stopped => "Arrêtée",
        SessionStatus.Failed => "Échec",
        _ => "Inconnu"
    };

    private static void Replace<T>(ObservableCollection<T> target, IReadOnlyList<T> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    /// <summary>
    /// Unsubscribes. A ViewModel that dies subscribed keeps being called by the coordinator for the
    /// lifetime of the application: a leak, and updates pushed into a dead visual tree. Called from
    /// the page's Unloaded.
    /// </summary>
    public void Dispose()
    {
        if (!_subscribed)
            return;

        _monitor.Changed -= OnSessionChanged;
        _monitor.LogAppended -= OnLogAppended;
        _subscribed = false;
    }
}
