using Microsoft.Extensions.Time.Testing;
using Nagare.Application.Abstractions;
using Nagare.Application.Channels;
using Nagare.Application.Media;
using Nagare.Application.Profiles;
using Nagare.Application.Streaming;
using Nagare.Domain.Channels;
using Nagare.Domain.Common;
using Nagare.Domain.Profiles;
using Nagare.Domain.Sessions;
using Nagare.ViewModels;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.ViewModels;

/// <summary>
/// The two non-negotiable rules of the real-time monitoring (plan §5) plus the environment gate.
/// No UI involved: the ViewModel only knows an IMediator, an IUiDispatcher and an ISessionMonitor.
/// </summary>
public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task Logs_are_bounded_to_500_lines()
    {
        var (vm, _, monitor, _, _) = await CreateLoadedAsync();

        // 501 lines: one more than the ring buffer holds.
        for (var i = 1; i <= 501; i++)
            monitor.RaiseLog($"line {i}");

        Assert.Equal(DashboardViewModel.MaxLogLines, vm.Logs.Count);
        Assert.Equal("line 2", vm.Logs[0]);      // the oldest one fell off
        Assert.Equal("line 501", vm.Logs[^1]);   // the newest one is there
    }

    [Fact]
    public async Task Logs_never_grow_unbounded_under_a_burst()
    {
        var (vm, _, monitor, _, _) = await CreateLoadedAsync();

        for (var i = 0; i < 5_000; i++)
            monitor.RaiseLog($"line {i}");

        Assert.Equal(DashboardViewModel.MaxLogLines, vm.Logs.Count);
    }

    [Fact]
    public async Task Logs_reach_the_collection_only_through_the_ui_thread()
    {
        // ISessionMonitor.LogAppended fires on the ffmpeg STDERR READER thread. Touching the
        // ObservableCollection from there is RPC_E_WRONG_THREAD in a real window — or, worse, a
        // silent corruption. The line MUST be marshalled.
        //
        // An INLINE dispatcher cannot see the difference: marshalled or not, the collection ends up
        // filled either way. That hole let a mutation deleting every Post() from the log path stay
        // green across all 24 view-model tests. A DEFERRED dispatcher makes the difference visible:
        // until the UI thread gets its turn, the collection must stay UNTOUCHED.
        var (vm, _, monitor, dispatcher, _) = await CreateLoadedAsync();
        dispatcher.Deferred = true;

        monitor.RaiseLog("line 1");
        monitor.RaiseLog("line 2");

        Assert.Empty(vm.Logs);   // the UI thread has not run yet: nothing may have been written

        dispatcher.Pump();

        Assert.Equal(new[] { "line 1", "line 2" }, vm.Logs);
    }

    [Fact]
    public async Task A_burst_of_log_lines_schedules_a_single_ui_callback()
    {
        // The other half of anti-freeze rule 1 (plan §5): the runner forwards EVERY ffmpeg line,
        // progress lines included. One dispatcher callback per line would flood the UI thread. The
        // lines are queued and a SINGLE drain is scheduled until it runs — invisible to an inline
        // dispatcher, where PostCount can only ever equal the number of lines.
        var (vm, _, monitor, dispatcher, _) = await CreateLoadedAsync();
        dispatcher.Deferred = true;

        for (var i = 1; i <= 200; i++)
            monitor.RaiseLog($"line {i}");

        Assert.Equal(1, dispatcher.PendingCount);   // 200 lines, ONE callback

        dispatcher.Pump();

        Assert.Equal(200, vm.Logs.Count);           // and not one line was lost
        Assert.Equal("line 200", vm.Logs[^1]);
    }

    [Fact]
    public async Task Stats_are_throttled_to_one_ui_update_per_second()
    {
        var (vm, time, monitor, dispatcher, _) = await CreateLoadedAsync();

        // First snapshot: a status change (none -> Running), published at once.
        monitor.RaiseChanged(Snapshot(SessionStatus.Running, fps: 30));
        var postsAfterFirst = dispatcher.PostCount;

        Assert.Equal(30, vm.Fps);

        // ffmpeg keeps emitting, several times per second. Same status: stats only.
        for (var i = 0; i < 20; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(40));
            monitor.RaiseChanged(Snapshot(SessionStatus.Running, fps: 100 + i));
        }

        // 800 ms of stats went by and NOT ONE of them reached the UI.
        Assert.Equal(postsAfterFirst, dispatcher.PostCount);
        Assert.Equal(30, vm.Fps);

        // Past the one-second window, the next one goes through.
        time.Advance(TimeSpan.FromMilliseconds(300));
        monitor.RaiseChanged(Snapshot(SessionStatus.Running, fps: 60));

        Assert.Equal(postsAfterFirst + 1, dispatcher.PostCount);
        Assert.Equal(60, vm.Fps);
    }

    [Fact]
    public async Task Status_change_bypasses_the_throttle()
    {
        var (vm, _, monitor, _, _) = await CreateLoadedAsync();

        monitor.RaiseChanged(Snapshot(SessionStatus.Running, fps: 30));

        // No time advanced at all: a stream drop must still show up instantly.
        monitor.RaiseChanged(Snapshot(SessionStatus.Reconnecting, fps: 30, reconnectAttempts: 1));

        Assert.Equal(SessionStatus.Reconnecting, vm.Status);
        Assert.Equal("Reconnexion", vm.StatusHeadline);
        Assert.Equal(1, vm.ReconnectAttempts);
        Assert.True(vm.IsSessionActive);
        Assert.True(vm.StopCommand.CanExecute(null));
    }

    [Fact]
    public async Task Health_warning_is_surfaced()
    {
        var (vm, _, monitor, _, _) = await CreateLoadedAsync();

        monitor.RaiseChanged(Snapshot(SessionStatus.Running, fps: 30, speed: 0.87, health: HealthIndicator.Warning));

        Assert.True(vm.IsHealthWarning);
        Assert.Equal(0.87, vm.Speed);
    }

    [Fact]
    public async Task Dispose_unsubscribes_from_the_monitor()
    {
        var (vm, _, monitor, dispatcher, _) = await CreateLoadedAsync();

        Assert.True(monitor.HasSubscribers);

        vm.Dispose();

        Assert.False(monitor.HasSubscribers);

        // A dead ViewModel hears nothing more: no leak, no update pushed into a dead visual tree.
        var postsBefore = dispatcher.PostCount;
        monitor.RaiseChanged(Snapshot(SessionStatus.Running, fps: 99));
        monitor.RaiseLog("orphan line");

        Assert.Equal(postsBefore, dispatcher.PostCount);
        Assert.NotEqual(99, vm.Fps);
        Assert.Empty(vm.Logs);
    }

    [Fact]
    public async Task Load_rehydrates_a_session_already_running()
    {
        var monitor = new FakeSessionMonitor { Current = Snapshot(SessionStatus.Running, fps: 42) };
        monitor.SeedLogs("older line", "last line");

        var (vm, _, _, _, _) = await CreateLoadedAsync(monitor);

        Assert.Equal(SessionStatus.Running, vm.Status);
        Assert.Equal(42, vm.Fps);
        Assert.Equal(new[] { "older line", "last line" }, vm.Logs);
    }

    // ------------------------------------------------------- preflight -> what is shown
    //
    // The RULE is tested in GetStartPreflightHandlerTests, at its own level. What follows tests the
    // ViewModel's remaining job: obey the verdict (button) and translate it (sentence).

    [Fact]
    public async Task Missing_ffmpeg_blocks_the_start_and_says_so_in_French()
    {
        var report = new FfmpegEnvironmentReport(
            FfmpegAvailable: false,
            FfprobeAvailable: false,
            FfmpegVersion: null,
            NvencAvailable: false,
            Error: "ffmpeg not found (configured path or PATH).");   // Infrastructure's words, in English

        var (vm, _, _, _, _) = await CreateLoadedAsync(environment: report);

        Assert.False(vm.StartCommand.CanExecute(null));

        // The user reads French, and reads about ffmpeg — not the probe's raw English error, and not
        // the name of a configuration key, which the ViewModel no longer knows.
        Assert.NotNull(vm.EnvironmentIssue);
        Assert.Contains("ffmpeg", vm.EnvironmentIssue);
        Assert.Contains("introuvable", vm.EnvironmentIssue);
        Assert.DoesNotContain("Nagare:Ffmpeg", vm.EnvironmentIssue);
    }

    [Fact]
    public async Task Missing_ffprobe_blocks_the_start()
    {
        var report = new FfmpegEnvironmentReport(
            FfmpegAvailable: true,
            FfprobeAvailable: false,
            FfmpegVersion: "7.1",
            NvencAvailable: true,
            Error: null);

        var (vm, _, _, _, _) = await CreateLoadedAsync(environment: report);

        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.NotNull(vm.EnvironmentIssue);
        Assert.Contains("ffprobe", vm.EnvironmentIssue);
    }

    /// <summary>A file ffprobe cannot read blocks the start, and is named as such next to the file.</summary>
    [Fact]
    public async Task An_unreadable_file_blocks_the_start()
    {
        var broken = new MediaValidationResult(
            Exists: true, Readable: false, Duration: null, Width: null, Height: null, Fps: null,
            VideoCodec: null, AudioCodec: null, Error: null);

        var (vm, _, _, _, _) = await CreateLoadedAsync(media: broken);

        vm.SelectedProfile = vm.Profiles.Single();
        vm.SelectedChannel = vm.Channels.Single();
        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.Equal("Fichier illisible par ffprobe.", vm.MediaError);
        Assert.Null(vm.MediaSummary);        // nothing to summarize about a file that would not open
        Assert.Null(vm.CommandPreview);      // and nothing to preview
    }

    [Fact]
    public async Task A_missing_file_blocks_the_start()
    {
        var missing = new MediaValidationResult(
            Exists: false, Readable: false, Duration: null, Width: null, Height: null, Fps: null,
            VideoCodec: null, AudioCodec: null, Error: "File not found.");

        var (vm, _, _, _, _) = await CreateLoadedAsync(media: missing);

        vm.SelectedProfile = vm.Profiles.Single();
        vm.SelectedChannel = vm.Channels.Single();
        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.Equal("Fichier introuvable.", vm.MediaError);
    }

    /// <summary>
    /// A running session holds the single slot (SPEC §5). The button goes off — and, deliberately,
    /// NO error is shouted: the status line already says a broadcast is on air.
    /// </summary>
    [Fact]
    public async Task A_running_session_blocks_a_second_start_without_shouting()
    {
        var (vm, _, monitor, _, _) = await CreateLoadedAsync();

        vm.SelectedProfile = vm.Profiles.Single();
        vm.SelectedChannel = vm.Channels.Single();
        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.True(vm.StartCommand.CanExecute(null));

        monitor.Current = Snapshot(SessionStatus.Running, fps: 30);
        monitor.RaiseChanged(monitor.Current);

        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.True(vm.StopCommand.CanExecute(null));
        Assert.Null(vm.EnvironmentIssue);
        Assert.Null(vm.MediaError);

        // The session ends: the slot is free again, and the button comes back on its own.
        monitor.Current = Snapshot(SessionStatus.Stopped);
        monitor.RaiseChanged(monitor.Current);

        Assert.True(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task An_nvenc_profile_without_nvenc_blocks_the_start()
    {
        var report = new FfmpegEnvironmentReport(
            FfmpegAvailable: true,
            FfprobeAvailable: true,
            FfmpegVersion: "7.1",
            NvencAvailable: false,
            Error: null);

        var (vm, _, _, _, _) = await CreateLoadedAsync(environment: report);

        Assert.Null(vm.EnvironmentIssue);   // nothing blocking yet: no profile selected

        vm.SelectedProfile = vm.Profiles.Single();   // an h264_nvenc profile

        Assert.NotNull(vm.EnvironmentIssue);
        Assert.Contains("NVENC", vm.EnvironmentIssue);
        Assert.False(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task Start_is_refused_until_file_profile_and_channel_are_chosen()
    {
        var (vm, _, _, _, _) = await CreateLoadedAsync();

        Assert.False(vm.StartCommand.CanExecute(null));

        vm.SelectedProfile = vm.Profiles.Single();
        vm.SelectedChannel = vm.Channels.Single();

        Assert.False(vm.StartCommand.CanExecute(null));   // still no input file

        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.True(vm.StartCommand.CanExecute(null));
    }

    // ------------------------------------------------- a blocked start always says why (UX §5)
    //
    // GetStartPreflightHandlerTests proves WHICH reason is reported. What follows proves the other
    // half of the acceptance criterion: whatever that reason is, SOMETHING is on screen. The three
    // surfaces are disjoint by design — environment, media, hint — so the assertion is on their
    // union, which is exactly what the user sees.

    [Theory]
    [InlineData(StartBlockReason.NotChecked)]
    [InlineData(StartBlockReason.FfmpegMissing)]
    [InlineData(StartBlockReason.FfprobeMissing)]
    [InlineData(StartBlockReason.NvencUnavailable)]
    [InlineData(StartBlockReason.SessionAlreadyActive)]
    [InlineData(StartBlockReason.ProfileNotSelected)]
    [InlineData(StartBlockReason.ChannelNotSelected)]
    [InlineData(StartBlockReason.InputFileNotSelected)]
    [InlineData(StartBlockReason.InputFileNotFound)]
    [InlineData(StartBlockReason.InputFileUnreadable)]
    public async Task Every_blocking_reason_puts_a_sentence_on_screen(StartBlockReason reason)
    {
        var (vm, _, _, _, _) = await CreateLoadedAsync();

        // Cleared first, and asserted cleared: StartPreflight is a record, so assigning a verdict
        // equal to the one already in place raises nothing and the sentences would simply be the
        // ones left over from load — the test would pass without the assignment doing anything.
        vm.Preflight = StartPreflight.Ready;
        Assert.Null(vm.EnvironmentIssue ?? vm.MediaError ?? vm.StartHint);

        vm.Preflight = new StartPreflight(reason);

        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.NotNull(vm.EnvironmentIssue ?? vm.MediaError ?? vm.StartHint);
    }

    [Fact]
    public async Task Nothing_is_said_once_the_start_is_cleared()
    {
        var (vm, _, _, _, _) = await CreateLoadedAsync();

        vm.SelectedProfile = vm.Profiles.Single();
        vm.SelectedChannel = vm.Channels.Single();
        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.True(vm.StartCommand.CanExecute(null));
        Assert.Null(vm.EnvironmentIssue);
        Assert.Null(vm.MediaError);
        Assert.Null(vm.StartHint);
    }

    /// <summary>The checklist reports the four launch conditions, one by one, as they are met.</summary>
    [Fact]
    public async Task The_checklist_fills_in_as_the_user_progresses()
    {
        var (vm, _, _, _, _) = await CreateLoadedAsync();

        Assert.True(vm.IsEnvironmentReady);    // a healthy toolchain was probed on load
        Assert.False(vm.IsProfileReady);
        Assert.False(vm.IsChannelReady);
        Assert.False(vm.IsFileReady);

        vm.SelectedProfile = vm.Profiles.Single();
        Assert.True(vm.IsProfileReady);

        vm.SelectedChannel = vm.Channels.Single();
        Assert.True(vm.IsChannelReady);

        await vm.PickFileCommand.ExecuteAsync(null);
        Assert.True(vm.IsFileReady);
    }

    /// <summary>
    /// The drop zone is the first thing on the page, so choosing the file BEFORE the profile is the
    /// natural order — and it is the one that used to break. The preflight re-answers
    /// <c>ProfileNotSelected</c>, a verdict equal to the one already held; the record raises no
    /// change, and the checklist was never told about the file.
    /// </summary>
    [Fact]
    public async Task A_file_chosen_before_the_profile_still_ticks_the_file_box()
    {
        var (vm, _, _, _, _) = await CreateLoadedAsync();

        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.Equal(InputFile, vm.InputFilePath);
        Assert.True(vm.IsFileReady);
        Assert.False(vm.IsProfileReady);
    }

    /// <summary>
    /// The preflight reports ONE reason, the first that holds. Deriving the boxes from it made them
    /// lie whenever another condition won the race.
    /// </summary>
    [Fact]
    public async Task A_box_reports_its_own_condition_not_the_winning_reason()
    {
        var report = new FfmpegEnvironmentReport(
            FfmpegAvailable: true, FfprobeAvailable: true, FfmpegVersion: "7.1",
            NvencAvailable: false, Error: null);

        var broken = new MediaValidationResult(
            Exists: true, Readable: false, Duration: null, Width: null, Height: null, Fps: null,
            VideoCodec: null, AudioCodec: null, Error: null);

        var (vm, _, _, _, _) = await CreateLoadedAsync(environment: report, media: broken);

        vm.SelectedProfile = vm.Profiles.Single();   // an h264_nvenc profile, on a machine without it
        await vm.PickFileCommand.ExecuteAsync(null);

        // The unreadable file is reported first, so NvencUnavailable never surfaces as the reason.
        Assert.Equal(StartBlockReason.InputFileUnreadable, vm.Preflight.Reason);

        Assert.False(vm.IsFileReady);
        Assert.False(vm.IsEnvironmentReady);   // the encoder is still missing, whatever won the race
    }

    [Fact]
    public async Task An_unreadable_file_does_not_tick_the_file_box()
    {
        var broken = new MediaValidationResult(
            Exists: true, Readable: false, Duration: null, Width: null, Height: null, Fps: null,
            VideoCodec: null, AudioCodec: null, Error: null);

        var (vm, _, _, _, _) = await CreateLoadedAsync(media: broken);

        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.False(vm.IsFileReady);
    }

    /// <summary>A machine without NVENC and an NVENC profile: the environment box goes off.</summary>
    [Fact]
    public async Task A_missing_encoder_unticks_the_environment_box()
    {
        var report = new FfmpegEnvironmentReport(
            FfmpegAvailable: true, FfprobeAvailable: true, FfmpegVersion: "7.1",
            NvencAvailable: false, Error: null);

        var (vm, _, _, _, _) = await CreateLoadedAsync(environment: report);

        Assert.True(vm.IsEnvironmentReady);

        vm.SelectedProfile = vm.Profiles.Single();   // an h264_nvenc profile

        Assert.False(vm.IsEnvironmentReady);
    }

    // ------------------------------------------------------------ a file dropped on the page

    [Fact]
    public async Task A_dropped_file_goes_through_the_same_validation_as_a_picked_one()
    {
        var (vm, _, _, _, _) = await CreateLoadedAsync();

        vm.SelectedProfile = vm.Profiles.Single();
        vm.SelectedChannel = vm.Channels.Single();

        await vm.UseFileCommand.ExecuteAsync(@"C:\videos\dropped.mp4");

        Assert.Equal(@"C:\videos\dropped.mp4", vm.InputFilePath);
        Assert.True(vm.IsFileReady);
        Assert.True(vm.StartCommand.CanExecute(null));
    }

    // ---------------------------------------------------------------- first run and end of run

    [Fact]
    public async Task A_blank_install_asks_for_a_profile_and_a_channel()
    {
        var (vm, _, _, _, _) = await CreateLoadedAsync(empty: true);

        Assert.False(vm.HasProfiles);
        Assert.False(vm.HasChannels);
        Assert.True(vm.NeedsSetup);
    }

    [Fact]
    public async Task A_configured_install_asks_for_nothing()
    {
        var (vm, _, _, _, _) = await CreateLoadedAsync();

        Assert.True(vm.HasProfiles);
        Assert.True(vm.HasChannels);
        Assert.False(vm.NeedsSetup);
    }

    /// <summary>
    /// A broadcast that ends leaves a report behind — otherwise the page simply empties out, and the
    /// last thing the user is left with is nothing (Peak-End rule).
    /// </summary>
    [Fact]
    public async Task A_finished_session_leaves_a_report()
    {
        var (vm, _, monitor, _, _) = await CreateLoadedAsync();

        monitor.RaiseChanged(Snapshot(SessionStatus.Running, fps: 30));
        Assert.Null(vm.SessionSummary);            // nothing to report while it is on air
        Assert.Equal("En direct", vm.StatusHeadline);
        Assert.Equal(StatusSeverity.Success, vm.Severity);

        monitor.RaiseChanged(Snapshot(SessionStatus.Stopped, reconnectAttempts: 2));

        Assert.NotNull(vm.SessionSummary);
        Assert.Contains("arrêtée", vm.SessionSummary);
        Assert.Contains("2 reconnexions", vm.SessionSummary);
        Assert.Equal(StatusSeverity.Neutral, vm.Severity);
    }

    /// <summary>
    /// ffmpeg counts drops per PROCESS and a reconnection starts a new one, so the last snapshot's
    /// counter is not the broadcast's total. Reporting it as such announced "0 image perdue" for the
    /// session that lost the most — the one the report exists for.
    /// </summary>
    [Fact]
    public async Task Drops_survive_a_reconnection_in_the_end_of_session_report()
    {
        var (vm, time, monitor, _, _) = await CreateLoadedAsync();

        monitor.RaiseChanged(Snapshot(SessionStatus.Running, fps: 30, drops: 1200));
        Assert.Equal(1200, vm.DroppedFrames);

        // The connection breaks: the coordinator forgets the dead process's stats on purpose.
        monitor.RaiseChanged(Snapshot(SessionStatus.Reconnecting, reconnectAttempts: 1, withStats: false));
        Assert.Equal(1200, vm.DroppedFrames);   // a snapshot without stats says nothing about drops

        // The relaunched ffmpeg counts from zero again.
        time.Advance(DashboardViewModel.StatsThrottle);
        monitor.RaiseChanged(Snapshot(SessionStatus.Running, fps: 30, reconnectAttempts: 1, drops: 5));
        Assert.Equal(1205, vm.DroppedFrames);

        monitor.RaiseChanged(Snapshot(SessionStatus.Stopped, reconnectAttempts: 1, drops: 5));

        Assert.Contains("1205 images perdues", vm.SessionSummary);
        Assert.Contains("1 reconnexion", vm.SessionSummary);
    }

    /// <summary>
    /// The health card answers "vers quoi ? avec quel fichier ?" — from what was LAUNCHED, not from
    /// the selection, which the user stays free to change while the broadcast runs.
    /// </summary>
    [Fact]
    public async Task The_live_card_keeps_the_channel_the_broadcast_was_started_with()
    {
        var (vm, _, monitor, _, _) = await CreateLoadedAsync();

        Assert.Null(vm.LiveChannelName);   // nothing launched: the card says nothing rather than a blank

        vm.SelectedProfile = vm.Profiles.Single();
        vm.SelectedChannel = vm.Channels.Single();
        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.StartCommand.ExecuteAsync(null);

        monitor.RaiseChanged(Snapshot(SessionStatus.Running, fps: 30));

        Assert.Equal(TestChannel.Name, vm.LiveChannelName);
        Assert.Equal(InputFile, vm.LiveInputFilePath);

        // The selection is the user's and may move; what is on air does not.
        vm.SelectedChannel = null;

        Assert.Equal(TestChannel.Name, vm.LiveChannelName);
    }

    [Fact]
    public async Task A_failed_session_reports_the_reason_the_domain_gave()
    {
        var (vm, _, monitor, _, _) = await CreateLoadedAsync();

        monitor.RaiseChanged(Snapshot(SessionStatus.Running, fps: 30));
        monitor.RaiseChanged(Snapshot(SessionStatus.Failed) with { LastError = "Connection refused." });

        Assert.Equal(StatusSeverity.Critical, vm.Severity);
        Assert.Contains("Connection refused.", vm.SessionSummary);
    }

    [Fact]
    public async Task A_degraded_broadcast_reads_as_a_warning_not_as_a_success()
    {
        var (vm, _, monitor, _, _) = await CreateLoadedAsync();

        monitor.RaiseChanged(Snapshot(SessionStatus.Running, speed: 0.8, health: HealthIndicator.Warning));

        Assert.Equal(StatusSeverity.Caution, vm.Severity);
        Assert.Equal("En direct", vm.StatusHeadline);   // the word does not change: the colour is not the message
    }

    /// <summary>What is shown before launching is the MASKED command line (SPEC §4).</summary>
    [Fact]
    public async Task Command_preview_is_the_masked_line()
    {
        var (vm, _, _, _, _) = await CreateLoadedAsync();

        vm.SelectedProfile = vm.Profiles.Single();
        vm.SelectedChannel = vm.Channels.Single();
        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.Equal(MaskedCommandLine, vm.CommandPreview);
        Assert.Contains(ProtectedStreamKey.Mask, vm.CommandPreview);
        Assert.DoesNotContain(FakeFfmpegCommandBuilder.StreamKey, vm.CommandPreview);
    }

    [Fact]
    public async Task Start_sends_the_selected_profile_channel_and_file()
    {
        var (vm, _, _, _, mediator) = await CreateLoadedAsync();

        vm.SelectedProfile = vm.Profiles.Single();
        vm.SelectedChannel = vm.Channels.Single();
        await vm.PickFileCommand.ExecuteAsync(null);

        await vm.StartCommand.ExecuteAsync(null);

        var command = mediator.Single<StartStreamCommand>();
        Assert.Equal(TestProfile.Id, command.ProfileId);
        Assert.Equal(TestChannel.Id, command.ChannelId);
        Assert.Equal(InputFile, command.InputFilePath);
    }

    // ------------------------------------------------------------------ fixtures

    private const string InputFile = @"C:\videos\input.mp4";

    private static readonly SessionId TestSession = SessionId.New();

    private static readonly string MaskedCommandLine =
        $"-re -stream_loop -1 -i {InputFile} -f flv rtmp://example.invalid/app/{ProtectedStreamKey.Mask}";

    private static readonly StreamProfileDto TestProfile = StreamProfileDto.From(
        StreamProfile.Create(
            "1080p NVENC",
            new EncodingSettings(VideoCodec.H264Nvenc, "p2", RateControl.Cbr, 3000, 3000, 3000, 60, 60, null, null),
            new AudioSettings(AudioCodec.Aac, 128, 48000),
            InputOptions.Default));

    private static readonly ChannelDto TestChannel = new(
        ChannelId.New(), "Twitch principal", Platform.Twitch, "rtmp://live.twitch.tv/app", KeyConfigured: true);

    private static readonly FfmpegEnvironmentReport HealthyEnvironment = new(
        FfmpegAvailable: true,
        FfprobeAvailable: true,
        FfmpegVersion: "7.1",
        NvencAvailable: true,
        Error: null);

    /// <param name="drops">Counter of the CURRENT ffmpeg process — it restarts at zero after each
    /// reconnection, which is the whole difficulty of the end-of-session tally.</param>
    /// <param name="withStats">False reproduces a snapshot the coordinator publishes with no stats
    /// at all, as it does while reconnecting.</param>
    private static SessionSnapshot Snapshot(
        SessionStatus status,
        double fps = 0,
        double speed = 1.0,
        int reconnectAttempts = 0,
        HealthIndicator health = HealthIndicator.Ok,
        int drops = 0,
        bool withStats = true)
        => new(
            TestSession,
            status,
            withStats
                ? new FfmpegStats(Frame: 100, Fps: fps, BitrateKbps: 3000, Speed: speed, DroppedFrames: drops, DupFrames: 0, Time: TimeSpan.FromSeconds(5))
                : null,
            health,
            reconnectAttempts,
            LastError: null);

    /// <summary>A loaded dashboard and the doubles behind it (positional: deconstructible).</summary>
    private sealed record Fixture(
        DashboardViewModel Vm,
        FakeTimeProvider Time,
        FakeSessionMonitor Monitor,
        FakeUiDispatcher Dispatcher,
        FakeMediator Mediator);

    private static readonly MediaValidationResult ValidMedia = new(
        Exists: true, Readable: true, Duration: TimeSpan.FromMinutes(3),
        Width: 1920, Height: 1080, Fps: 30, VideoCodec: "h264", AudioCodec: "aac", Error: null);

    private static async Task<Fixture> CreateLoadedAsync(
        FakeSessionMonitor? monitor = null,
        FfmpegEnvironmentReport? environment = null,
        MediaValidationResult? media = null,
        bool empty = false)
    {
        monitor ??= new FakeSessionMonitor();
        var time = new FakeTimeProvider();
        var dispatcher = new FakeUiDispatcher();

        // "empty" is the first run: no profile, no channel — the state the page has to guide out of.
        IReadOnlyList<StreamProfileDto> profiles = empty ? [] : [TestProfile];
        IReadOnlyList<ChannelDto> channels = empty ? [] : [TestChannel];

        // The preflight is answered by the REAL Application handler, not by a canned verdict. The
        // rule is tested on its own (GetStartPreflightHandlerTests); what these tests must prove is
        // that the ViewModel asks it, obeys it, and translates it — so the thing they talk to has to
        // be the thing that decides.
        var preflight = new GetStartPreflightHandler(monitor);

        var mediator = new FakeMediator()
            .Answer<GetFfmpegEnvironmentQuery>(environment ?? HealthyEnvironment)
            .Answer<GetStreamProfilesQuery>(profiles)
            .Answer<GetChannelsQuery>(channels)
            .Answer<ValidateMediaFileQuery>(media ?? ValidMedia)
            .Answer<GetStartPreflightQuery>(query => preflight.Handle(query).GetAwaiter().GetResult())
            .Answer<BuildCommandPreviewQuery>(MaskedCommandLine)
            .Answer<StartStreamCommand>(TestSession);

        var vm = new DashboardViewModel(mediator, monitor, dispatcher, new FakeVideoFilePicker(InputFile), time);
        await vm.LoadCommand.ExecuteAsync(null);

        return new Fixture(vm, time, monitor, dispatcher, mediator);
    }
}
