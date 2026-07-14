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
using Nagare.Presentation.ViewModels;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.Presentation;

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
        Assert.Equal("Reconnexion", vm.StatusLabel);
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

    [Fact]
    public async Task Missing_ffmpeg_blocks_the_start()
    {
        var report = new FfmpegEnvironmentReport(
            FfmpegAvailable: false,
            FfprobeAvailable: false,
            FfmpegVersion: null,
            NvencAvailable: false,
            Error: "ffmpeg introuvable.");

        var (vm, _, _, _, _) = await CreateLoadedAsync(environment: report);

        Assert.Equal("ffmpeg introuvable.", vm.EnvironmentIssue);
        Assert.False(vm.StartCommand.CanExecute(null));
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

    private static SessionSnapshot Snapshot(
        SessionStatus status,
        double fps = 0,
        double speed = 1.0,
        int reconnectAttempts = 0,
        HealthIndicator health = HealthIndicator.Ok)
        => new(
            TestSession,
            status,
            new FfmpegStats(Frame: 100, Fps: fps, BitrateKbps: 3000, Speed: speed, DroppedFrames: 0, DupFrames: 0, Time: TimeSpan.FromSeconds(5)),
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

    private static async Task<Fixture> CreateLoadedAsync(
        FakeSessionMonitor? monitor = null,
        FfmpegEnvironmentReport? environment = null)
    {
        monitor ??= new FakeSessionMonitor();
        var time = new FakeTimeProvider();
        var dispatcher = new FakeUiDispatcher();

        IReadOnlyList<StreamProfileDto> profiles = [TestProfile];
        IReadOnlyList<ChannelDto> channels = [TestChannel];

        var mediator = new FakeMediator()
            .Answer<GetFfmpegEnvironmentQuery>(environment ?? HealthyEnvironment)
            .Answer<GetStreamProfilesQuery>(profiles)
            .Answer<GetChannelsQuery>(channels)
            .Answer<ValidateMediaFileQuery>(new MediaValidationResult(
                Exists: true, Readable: true, Duration: TimeSpan.FromMinutes(3),
                Width: 1920, Height: 1080, Fps: 30, VideoCodec: "h264", AudioCodec: "aac", Error: null))
            .Answer<BuildCommandPreviewQuery>(MaskedCommandLine)
            .Answer<StartStreamCommand>(TestSession);

        var vm = new DashboardViewModel(mediator, monitor, dispatcher, new FakeVideoFilePicker(InputFile), time);
        await vm.LoadCommand.ExecuteAsync(null);

        return new Fixture(vm, time, monitor, dispatcher, mediator);
    }
}
