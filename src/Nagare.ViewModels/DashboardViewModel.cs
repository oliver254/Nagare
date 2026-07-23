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
using Nagare.ViewModels.Abstractions;

namespace Nagare.ViewModels;

/// <summary>
/// The broadcast page: choose file / profile / channel, preview the ffmpeg command (key masked —
/// SPEC §4), start, stop, and watch the session live (plan §5).
///
/// NO RULE LIVES HERE. Whether a broadcast may start is decided by
/// <see cref="GetStartPreflightQuery"/> in the Application layer, which answers a structured
/// <see cref="StartBlockReason"/>. This class does two things with it: it exposes
/// <c>CanStart</c>, and it TRANSLATES the reason into the French sentence shown on screen. The
/// wording is a UI concern; the rule is not — and it used to sit right here, in French, quoting an
/// ffmpeg configuration key by name.
///
/// THREADING — the other half of this class. <see cref="ISessionMonitor"/> raises
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

    /// <summary>
    /// The two EXPENSIVE facts the preflight decides on, gathered once each and cached here: the
    /// environment costs three process launches, the media report one. They are inputs to the rule,
    /// not the rule — see <see cref="GetStartPreflightQuery"/>.
    /// </summary>
    private FfmpegEnvironmentReport? _environment;

    private MediaValidationResult? _media;

    private bool _subscribed;

    // Throttle state. Touched ONLY from OnSessionChanged, which the coordinator calls from its
    // single mailbox loop thread: no lock needed, and none would help.
    private SessionSnapshot? _lastObserved;
    private DateTimeOffset _lastPublishedAt;

    /// <summary>
    /// Drop counting across the whole broadcast. UI thread only.
    ///
    /// <para>ffmpeg counts drops per PROCESS, and every reconnection starts a new one — the
    /// coordinator even forgets the corpse's stats on purpose, so its health indicator is not fooled
    /// by a counter that restarts at zero. Reporting <c>snapshot.Stats.DroppedFrames</c> as the
    /// session total therefore announced "0 image perdue" for a broadcast that lost thousands before
    /// dropping the connection — the one case the end-of-session report exists for.</para>
    ///
    /// <para>A sample smaller than the previous one is a restart: the peak reached by the process
    /// that died is banked, and counting resumes from the new one.</para>
    /// </summary>
    private int _dropsBanked;

    private int _lastDropSample;

    /// <summary>Id of the session the tally above belongs to. UI thread only.</summary>
    private SessionId? _lastApplied;

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
    private StreamProfileDto? _selectedProfile;

    [ObservableProperty]
    private ChannelDto? _selectedChannel;

    [ObservableProperty]
    private string? _inputFilePath;

    /// <summary>
    /// Optional maximum duration, in DECIMAL hours (0,5 = 30 min): null = broadcast until stopped.
    /// Hours are a UI unit — the model handles a <see cref="TimeSpan"/> (ADR-0009 §4). The bounds
    /// are NOT restated here: <see cref="StreamSession.MaxAllowedDuration"/> is what the input is
    /// bounded with, and anything that gets through anyway is refused by the domain.
    /// </summary>
    [ObservableProperty]
    private double? _durationHours;

    /// <summary>What the command carries. Null hours = null duration = no limit.</summary>
    private TimeSpan? MaxDuration => DurationHours is { } hours ? TimeSpan.FromHours(hours) : null;

    /// <summary>
    /// Upper bound of the duration field, in hours — sourced from the domain
    /// (<see cref="StreamSession.MaxAllowedDuration"/>), never restated in the view. The input cap and
    /// the invariant the domain enforces are then the SAME number by construction: raise the domain
    /// constant and the field follows, so the two can never disagree (ADR-0009 §1).
    /// </summary>
    public double MaxDurationHours => StreamSession.MaxAllowedDuration.TotalHours;

    /// <summary>
    /// The local clock time the broadcast would stop at, recomputed as the duration is typed — the
    /// end time US-0 wants shown BEFORE launch. Null when no duration is set (broadcast until stopped).
    /// It is a preview off "now": the authoritative end time is fixed at launch and read back from
    /// <see cref="SessionSnapshot.PlannedEndsAt"/> (see <see cref="PlannedEndLabel"/>).
    /// </summary>
    [ObservableProperty]
    private string? _durationEndPreview;

    partial void OnDurationHoursChanged(double? value)
        => DurationEndPreview = value is { } hours && hours > 0
            ? $"Se termine automatiquement vers {LocalClock(_time.GetUtcNow() + TimeSpan.FromHours(hours))}"
            : null;

    /// <summary>ffprobe report of the chosen file: duration, resolution, codecs.</summary>
    [ObservableProperty]
    private string? _mediaSummary;

    /// <summary>Translation of a media <see cref="StartBlockReason"/>. Null = the file is fine, or none is chosen.</summary>
    [ObservableProperty]
    private string? _mediaError;

    /// <summary>Generated ffmpeg command line, KEY MASKED (SPEC §4). Comes from the domain, never rebuilt here.</summary>
    [ObservableProperty]
    private string? _commandPreview;

    /// <summary>
    /// Translation of an environment <see cref="StartBlockReason"/> (ffmpeg/ffprobe missing, NVENC
    /// required but absent). Null = nothing of the sort blocks. A displayed sentence, never a rule.
    /// </summary>
    [ObservableProperty]
    private string? _environmentIssue;

    /// <summary>
    /// The verdict, and the ONLY thing <see cref="CanStart"/> reads. It starts at
    /// <see cref="StartPreflight.NotChecked"/> — no verdict means no start, which is also what keeps
    /// the button off during the instant a fresh check is in flight.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private StartPreflight _preflight = StartPreflight.NotChecked;

    // ------------------------------------------------------------ launch checklist
    //
    // A disabled button that does not say what is missing is the worst hole of this page. The four
    // flags below and <see cref="StartHint"/> close it: TOGETHER with EnvironmentIssue and
    // MediaError they cover all ten StartBlockReason values, so a blocked start always shows a
    // reason without a click. They report the FACTS; they do not decide anything.

    [ObservableProperty]
    private bool _isEnvironmentReady;

    [ObservableProperty]
    private bool _isFileReady;

    [ObservableProperty]
    private bool _isProfileReady;

    [ObservableProperty]
    private bool _isChannelReady;

    /// <summary>
    /// The blocking reasons that are neither a toolchain fault nor a file fault — "you have not
    /// picked a channel yet". Shown next to the button, quietly: it is a next step, not an error.
    /// </summary>
    [ObservableProperty]
    private string? _startHint;

    // ------------------------------------------------------------- first-run state

    [ObservableProperty]
    private bool _hasProfiles;

    [ObservableProperty]
    private bool _hasChannels;

    /// <summary>No profile or no channel: nothing can be broadcast yet, and the page must say so.</summary>
    [ObservableProperty]
    private bool _needsSetup = true;

    // --------------------------------------------------------------- live session

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand))]
    private bool _isSessionActive;

    /// <summary>
    /// The word shown on the health badge. <see cref="LabelOf"/> everywhere except on air, where the
    /// streamer's word wins: "En direct", not "En cours".
    /// </summary>
    [ObservableProperty]
    private string _statusHeadline = "Aucune session";

    /// <summary>
    /// Channel and file of the RUNNING broadcast — captured at start, not read from the selection.
    ///
    /// <para>The selection belongs to the user and is empty on a page that was rebuilt while ffmpeg
    /// kept going (navigate away and back: a new ViewModel, then rehydration from
    /// <see cref="ISessionMonitor"/>). Binding the health card to it left "En direct" above a blank
    /// channel and a blank path — the two questions that card exists to answer.</para>
    ///
    /// <para>Null when the session was not started from this instance: the card then says nothing
    /// rather than something stale. <see cref="SessionSnapshot"/> carries neither, so recovering them
    /// on rehydration needs an Application change — see docs/design/ux-ui.md §9.</para>
    /// </summary>
    [ObservableProperty]
    private string? _liveChannelName;

    [ObservableProperty]
    private string? _liveInputFilePath;

    /// <summary>
    /// When the RUNNING broadcast has a maximum duration, the local time it will stop itself at
    /// (ADR-0009, US-0). Fixed at launch — read from <see cref="SessionSnapshot.PlannedEndsAt"/>, not
    /// recomputed here. Null for an unbounded broadcast, and once the session has ended. Shown on the
    /// health card, which is itself only visible while a session is active.
    /// </summary>
    [ObservableProperty]
    private string? _plannedEndLabel;

    /// <summary>How loud that badge reads. Paired with the word above — never colour alone.</summary>
    [ObservableProperty]
    private StatusSeverity _severity = StatusSeverity.Neutral;

    /// <summary>
    /// What the broadcast left behind, shown once it ends: drops, reconnections, and the reason when
    /// it failed. Without it a session — a failed one above all — ends on an empty screen.
    ///
    /// <para>It carries NO duration: <see cref="SessionSnapshot"/> has neither a start time nor an
    /// elapsed one, and ffmpeg's own <c>time=</c> resets at every reconnection. See
    /// docs/design/ux-ui.md §9.</para>
    /// </summary>
    [ObservableProperty]
    private string? _sessionSummary;

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
        await RefreshPreflightAsync();

        var profiles = await _mediator.SendAsync<GetStreamProfilesQuery, IReadOnlyList<StreamProfileDto>>(
            new GetStreamProfilesQuery());
        Replace(Profiles, profiles);

        var channels = await _mediator.SendAsync<GetChannelsQuery, IReadOnlyList<ChannelDto>>(
            new GetChannelsQuery());
        Replace(Channels, channels);

        // A blank install has neither, and the page is then a dead end unless it says where to go.
        HasProfiles = Profiles.Count > 0;
        HasChannels = Channels.Count > 0;
        NeedsSetup = !HasProfiles || !HasChannels;

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

        await ApplyFileAsync(path);
    });

    /// <summary>
    /// Same as picking, for a file DROPPED on the page. Liberal in what it accepts (Postel): the
    /// extension is not filtered here — ffprobe answers whether the file can be broadcast, and the
    /// preflight decides. A UI-side whitelist would be a second, poorer rule.
    /// </summary>
    [RelayCommand]
    private Task UseFileAsync(string path) => RunGuardedAsync(() => ApplyFileAsync(path));

    private async Task ApplyFileAsync(string path)
    {
        InputFilePath = path;
        _media = null;                  // the previous file's report says nothing about this one

        await ValidateFileAsync();      // fills _media and the summary
        await RefreshPreflightAsync();  // the verdict on that report — and with it, MediaError
        await RefreshPreviewAsync();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartAsync() => RunGuardedAsync(async () =>
    {
        // A new session shows its own lines only — the coordinator clears its buffer too.
        Logs.Clear();
        _incomingLogs.Clear();

        // Captured BEFORE the dispatch: from here on the health card answers "vers quoi ? avec quel
        // fichier ?" from what was actually launched, not from a selection the user may still change.
        LiveChannelName = SelectedChannel!.Name;
        LiveInputFilePath = InputFilePath;

        await _mediator.DispatchAsync<StartStreamCommand, SessionId>(
            new StartStreamCommand(SelectedProfile!.Id, SelectedChannel.Id, InputFilePath!, MaxDuration));
    });

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync() => RunGuardedAsync(() => _mediator.DispatchAsync(new StopStreamCommand()));

    /// <summary>The verdict, and nothing else. The rule that produced it lives in Application.</summary>
    private bool CanStart() => Preflight.CanStart;

    private bool CanStop() => IsSessionActive;

    // ------------------------------------------------------------ selection effects

    partial void OnSelectedProfileChanged(StreamProfileDto? value)
    {
        QueuePreflightRefresh();   // an NVENC profile on a machine without NVENC blocks the start
        QueuePreviewRefresh();
    }

    partial void OnSelectedChannelChanged(ChannelDto? value)
    {
        QueuePreflightRefresh();
        QueuePreviewRefresh();
    }

    /// <summary>A session that starts or stops flips the verdict: the slot is taken, or freed.</summary>
    partial void OnIsSessionActiveChanged(bool value) => QueuePreflightRefresh();

    /// <summary>The sentences shown on screen follow the verdict — they are its translation.</summary>
    partial void OnPreflightChanged(StartPreflight value) => ApplyVerdict(value.Reason);

    /// <summary>
    /// Puts the verdict on screen. Called EXPLICITLY after every assignment of
    /// <see cref="Preflight"/>, and not only from its changed-hook.
    ///
    /// <para><see cref="StartPreflight"/> is a record, so a new verdict equal to the previous one
    /// never raises PropertyChanged and the hook never runs. That is invisible when the reason
    /// changes — and silent when it does not: picking a file while no profile is selected re-answers
    /// <c>ProfileNotSelected</c>, so the checklist kept "Fichier" unticked for a file that was
    /// loaded and valid. The sentences are cheap and idempotent; running them twice costs nothing,
    /// not running them at all cost the whole feature.</para>
    /// </summary>
    private void ApplyVerdict(StartBlockReason reason)
    {
        EnvironmentIssue = EnvironmentMessage(reason);
        MediaError = MediaMessage(reason);
        StartHint = StartHintMessage(reason);
        RefreshChecklist();
    }

    /// <summary>
    /// The four launch conditions, read from the FACTS they each stand for — not from the verdict.
    ///
    /// <para>The preflight reports one reason, the first that holds, so deriving four boxes from it
    /// made a box tick green whenever another condition won the race: a machine without NVENC and an
    /// unreadable file reports <c>InputFileUnreadable</c>, and "Environnement" then ticked although
    /// the selected profile cannot encode here. Every fact below is available locally; none of them
    /// needs the arbitration.</para>
    ///
    /// <para>Nothing here decides whether a start is allowed — <see cref="Preflight"/> alone does,
    /// and it is what drives the button.</para>
    /// </summary>
    private void RefreshChecklist()
    {
        IsEnvironmentReady = _environment is { FfmpegAvailable: true, FfprobeAvailable: true }
            && (SelectedProfile?.Video.Codec.RequiresNvenc() is not true || _environment.NvencAvailable);

        IsProfileReady = SelectedProfile is not null;
        IsChannelReady = SelectedChannel is not null;

        IsFileReady = !string.IsNullOrWhiteSpace(InputFilePath)
            && _media is { Exists: true, Readable: true, Error: null };
    }

    /// <summary>
    /// Fire-and-forget refresh triggered by a selection change. Safe: the body cannot throw — it is
    /// wrapped — and it runs on the UI thread, where the setter fired.
    /// </summary>
    private void QueuePreviewRefresh() => _ = RefreshPreviewAsync();

    /// <summary>
    /// Same, for the verdict — with one twist: the old verdict is dropped FIRST. It was formed on a
    /// selection that no longer exists, and leaving it in place would keep the Start button lit for
    /// the instant the query takes to answer — long enough to click it.
    /// </summary>
    private void QueuePreflightRefresh()
    {
        Preflight = StartPreflight.NotChecked;
        _ = RefreshPreflightAsync();
    }

    /// <summary>
    /// Asks Application whether a start is allowed. Every fact it decides on is passed in: the two
    /// costly reports are cached here (gathering them on each keystroke would spawn four processes),
    /// the live session state it reads for itself. Caching is plumbing; deciding is the rule.
    /// </summary>
    private async Task RefreshPreflightAsync()
    {
        try
        {
            Preflight = await _mediator.SendAsync<GetStartPreflightQuery, StartPreflight>(
                new GetStartPreflightQuery(_environment, SelectedProfile, SelectedChannel, InputFilePath, _media));
        }
        catch (Exception ex)
        {
            // No verdict = no start. Failing open here would offer a button that cannot work.
            Preflight = StartPreflight.NotChecked;
            ErrorMessage = ex.Message;
        }

        // Unconditional: a verdict equal to the previous one raises nothing — see ApplyVerdict.
        ApplyVerdict(Preflight.Reason);
    }

    /// <summary>
    /// Runs ffprobe on the chosen file and keeps the report. It does NOT judge it: whether an
    /// unreadable file blocks the start is the preflight's call, and <see cref="MediaError"/> is
    /// that call's translation.
    /// </summary>
    private async Task ValidateFileAsync()
    {
        _media = await _mediator.SendAsync<ValidateMediaFileQuery, MediaValidationResult>(
            new ValidateMediaFileQuery(InputFilePath!));

        MediaSummary = Summarize(_media);
    }

    /// <summary>
    /// Duration · resolution · fps · codecs, as far as ffprobe got. A file it could not decode has
    /// none of them, and the summary comes out empty — no need to restate the rule to know there is
    /// nothing to show.
    /// </summary>
    private static string? Summarize(MediaValidationResult result)
    {
        var summary = string.Join(" · ", new[]
        {
            result.Duration is { } duration ? duration.ToString(@"hh\:mm\:ss") : null,
            result.Width is { } width && result.Height is { } height ? $"{width}×{height}" : null,
            result.Fps is { } fps ? $"{fps:0.##} fps" : null,
            result.VideoCodec,
            result.AudioCodec
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return string.IsNullOrEmpty(summary) ? null : summary;
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

    // --------------------------------------------------------- reason -> French sentence

    /// <summary>
    /// The toolchain reasons, as shown in the environment InfoBar. Everything else returns null:
    /// "you have not picked a profile yet" is not an error to shout about — the disabled button says
    /// it already.
    ///
    /// <para>No configuration key is named here. Which key carries the ffmpeg path is Infrastructure's
    /// business, it changes with the configuration schema, and a ViewModel that quotes it is a
    /// ViewModel that lies the day it moves.</para>
    /// </summary>
    private static string? EnvironmentMessage(StartBlockReason reason) => reason switch
    {
        StartBlockReason.FfmpegMissing =>
            "ffmpeg est introuvable. Renseignez son chemin dans la configuration de l'application, "
            + "ou ajoutez ffmpeg au PATH.",

        StartBlockReason.FfprobeMissing =>
            "ffprobe est introuvable : la validation des fichiers vidéo est impossible.",

        StartBlockReason.NvencUnavailable =>
            "Le profil sélectionné exige NVENC, indisponible sur cette machine. "
            + "Choisissez un profil libx264.",

        _ => null
    };

    /// <summary>The reasons that concern the chosen file, shown next to it.</summary>
    private static string? MediaMessage(StartBlockReason reason) => reason switch
    {
        StartBlockReason.InputFileNotFound => "Fichier introuvable.",
        StartBlockReason.InputFileUnreadable => "Fichier illisible par ffprobe.",
        _ => null
    };

    /// <summary>
    /// The remaining five reasons — the ones nothing else covers. They used to return null, which is
    /// how the page ended up with a dead Start button and not a word to explain it. They are stated
    /// flatly, next to the button: they name the next step, they do not scold.
    /// </summary>
    private static string? StartHintMessage(StartBlockReason reason) => reason switch
    {
        StartBlockReason.NotChecked => "Vérification en cours…",
        StartBlockReason.SessionAlreadyActive => "Une diffusion est déjà en cours.",
        StartBlockReason.ProfileNotSelected => "Choisissez un profil d'encodage.",
        StartBlockReason.ChannelNotSelected => "Choisissez un channel de diffusion.",
        StartBlockReason.InputFileNotSelected => "Choisissez la vidéo à diffuser.",
        _ => null
    };

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
        // Read BEFORE the flag is overwritten: the transition active -> not active is what produces
        // the end-of-session summary, and it is visible for one call only.
        var wasActive = IsSessionActive;

        // Same reason, one step earlier: a session that is REPLACED (a new id) must not inherit the
        // previous one's drop tally.
        if (_lastApplied is { } applied && applied != snapshot.Id)
            ResetDropTally();

        _lastApplied = snapshot.Id;

        Status = snapshot.Status;

        // "Active" is the domain's word, not ours (SessionStatusExtensions): the coordinator refuses
        // a second start on that very predicate. Restating it here as "Starting or Running or
        // Reconnecting" was a second definition of the same rule, free to drift.
        IsSessionActive = snapshot.Status.IsActive();

        // The automatic-stop time, only while a session is on air. PlannedEndsAt lingers on the
        // terminal snapshot (the coordinator keeps it so the ended session stays displayable), so it
        // is gated on IsSessionActive here — a stopped broadcast has no future stop to announce.
        PlannedEndLabel = IsSessionActive && snapshot.PlannedEndsAt is { } plannedEnd
            ? $"Arrêt automatique à {LocalClock(plannedEnd)}"
            : null;

        ReconnectAttempts = snapshot.ReconnectAttempts;
        LastError = snapshot.LastError;
        IsHealthWarning = snapshot.Health == HealthIndicator.Warning;

        Fps = snapshot.Stats?.Fps ?? 0;
        BitrateKbps = snapshot.Stats?.BitrateKbps ?? 0;
        Speed = snapshot.Stats?.Speed ?? 0;
        DroppedFrames = AccumulateDrops(snapshot.Stats?.DroppedFrames);

        StatusHeadline = HeadlineOf(snapshot.Status);
        Severity = SeverityOf(snapshot.Status, IsHealthWarning);

        if (IsSessionActive)
            SessionSummary = null;            // a new broadcast buries the previous one's report
        else if (wasActive)
            SessionSummary = Summarize(snapshot, DroppedFrames);
    }

    /// <summary>
    /// Total drops of the broadcast, across the successive ffmpeg processes — see
    /// <see cref="_dropsBanked"/>. A null sample is a snapshot with no stats at all (the coordinator
    /// forgets them while reconnecting): it says nothing about the count, so it changes nothing.
    /// </summary>
    private int AccumulateDrops(int? sample)
    {
        if (sample is not { } drops)
            return _dropsBanked + _lastDropSample;

        if (drops < _lastDropSample)
            _dropsBanked += _lastDropSample;   // the process that held that peak is gone

        _lastDropSample = drops;
        return _dropsBanked + drops;
    }

    private void ResetDropTally()
    {
        _dropsBanked = 0;
        _lastDropSample = 0;
    }

    /// <summary>What the badge says. Only <c>Running</c> differs from <see cref="LabelOf"/>.</summary>
    private static string HeadlineOf(SessionStatus status)
        => status is SessionStatus.Running ? "En direct" : LabelOf(status);

    /// <summary>
    /// How loud it reads. A healthy broadcast is green, a degraded one amber — the health indicator
    /// is what separates them, and it is the domain's (SPEC §6), computed from speed and drops.
    /// </summary>
    private static StatusSeverity SeverityOf(SessionStatus status, bool healthWarning) => status switch
    {
        SessionStatus.Starting => StatusSeverity.Information,
        SessionStatus.Running => healthWarning ? StatusSeverity.Caution : StatusSeverity.Success,
        SessionStatus.Reconnecting => StatusSeverity.Caution,
        SessionStatus.Failed => StatusSeverity.Critical,
        _ => StatusSeverity.Neutral
    };

    /// <summary>
    /// The last thing seen of a broadcast. A failure names its cause — the domain's own text, never
    /// re-authored here.
    /// </summary>
    /// <param name="drops">Total of the whole broadcast — see <see cref="AccumulateDrops"/>. Taking
    /// it from the snapshot would report the LAST ffmpeg process's counter as the session's.</param>
    private static string Summarize(SessionSnapshot snapshot, int drops)
    {
        var tail = $"{Plural(drops, "image perdue", "images perdues")}, "
            + $"{Plural(snapshot.ReconnectAttempts, "reconnexion", "reconnexions")}.";

        return snapshot.Status switch
        {
            SessionStatus.Failed => $"Diffusion interrompue — {snapshot.LastError ?? "cause inconnue"}. {tail}",

            // US-0: a broadcast that stopped because its maximum duration was reached says so, so the
            // user tells "it ran its course" apart from "I hit Stop" without reading the logs.
            _ when snapshot.StopReason is SessionStopReason.DurationElapsed
                => $"Diffusion arrêtée automatiquement (durée atteinte) — {tail}",

            _ => $"Diffusion arrêtée — {tail}"
        };
    }

    private static string Plural(int count, string singular, string plural)
        => $"{count} {(count > 1 ? plural : singular)}";

    /// <summary>
    /// Local wall-clock HH:mm of an instant — the automatic-stop time. Local by design: the app is
    /// single-user on one machine, and the schedule is read in the machine's own time (arbitrage F).
    /// </summary>
    private static string LocalClock(DateTimeOffset instant) => instant.ToLocalTime().ToString("HH:mm");

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
