using Nagare.Application.Abstractions;
using Nagare.Application.Channels;
using Nagare.Application.Profiles;
using Nagare.Application.Streaming;
using Nagare.Domain.Channels;
using Nagare.Domain.Common;
using Nagare.Domain.Profiles;
using Nagare.Domain.Sessions;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.Application.Streaming;

/// <summary>
/// The start policy — "may this broadcast start, and if not, why?" — tested where it now lives.
///
/// It used to be written in French sentences inside the dashboard ViewModel, which meant it could
/// only ever be exercised through a ViewModel, a fake dispatcher and a fake mediator. Here it is a
/// function of five facts, and each rule gets its own case.
/// </summary>
public sealed class GetStartPreflightHandlerTests
{
    // ------------------------------------------------------------------ environment

    [Fact]
    public async Task An_unprobed_environment_yields_no_verdict()
    {
        // Not "startable": nothing has been checked yet. The distinction is the point — a missing
        // verdict must never read as a green light. Built by hand: the fixture below defaults the
        // environment to a healthy one, and a null environment is precisely what this case is about.
        var handler = new GetStartPreflightHandler(new FakeSessionMonitor());

        var preflight = await handler.Handle(
            new GetStartPreflightQuery(Environment: null, Profile: null, Channel: null, InputFilePath: null, Media: null));

        Assert.Equal(StartBlockReason.NotChecked, preflight.Reason);
        Assert.False(preflight.CanStart);
    }

    [Fact]
    public async Task Missing_ffmpeg_blocks_the_start()
    {
        var preflight = await Evaluate(environment: EnvironmentReport(ffmpeg: false, ffprobe: false));

        Assert.Equal(StartBlockReason.FfmpegMissing, preflight.Reason);
        Assert.False(preflight.CanStart);
    }

    [Fact]
    public async Task Missing_ffprobe_blocks_the_start()
    {
        var preflight = await Evaluate(environment: EnvironmentReport(ffmpeg: true, ffprobe: false));

        Assert.Equal(StartBlockReason.FfprobeMissing, preflight.Reason);
    }

    /// <summary>A broken toolchain outranks an unfinished selection: fix ffmpeg first.</summary>
    [Fact]
    public async Task Missing_ffmpeg_outranks_an_empty_selection()
    {
        var preflight = await Evaluate(
            environment: EnvironmentReport(ffmpeg: false, ffprobe: false),
            profile: null,
            channel: null,
            inputFilePath: null);

        Assert.Equal(StartBlockReason.FfmpegMissing, preflight.Reason);
    }

    // ------------------------------------------------------------------------ NVENC

    [Fact]
    public async Task An_nvenc_profile_on_a_machine_without_nvenc_blocks_the_start()
    {
        var preflight = await Evaluate(
            environment: EnvironmentReport(nvenc: false),
            profile: NvencProfile,
            channel: TestChannel,
            inputFilePath: InputFile,
            media: ValidMedia);

        Assert.Equal(StartBlockReason.NvencUnavailable, preflight.Reason);
        Assert.False(preflight.CanStart);
    }

    [Fact]
    public async Task An_nvenc_profile_on_a_machine_with_nvenc_starts()
    {
        var preflight = await Evaluate(
            environment: EnvironmentReport(nvenc: true),
            profile: NvencProfile,
            channel: TestChannel,
            inputFilePath: InputFile,
            media: ValidMedia);

        Assert.True(preflight.CanStart);
    }

    /// <summary>The mirror case, and the reason the rule cannot be "NVENC must be available".</summary>
    [Fact]
    public async Task A_libx264_profile_does_not_need_nvenc()
    {
        var preflight = await Evaluate(
            environment: EnvironmentReport(nvenc: false),
            profile: Libx264Profile,
            channel: TestChannel,
            inputFilePath: InputFile,
            media: ValidMedia);

        Assert.Equal(StartBlockReason.None, preflight.Reason);
        Assert.True(preflight.CanStart);
    }

    /// <summary>Nothing is selected yet: NVENC cannot be the complaint.</summary>
    [Fact]
    public async Task No_profile_selected_is_not_an_nvenc_problem()
    {
        var preflight = await Evaluate(environment: EnvironmentReport(nvenc: false), profile: null);

        Assert.Equal(StartBlockReason.ProfileNotSelected, preflight.Reason);
    }

    // ---------------------------------------------------------------- single session

    [Theory]
    [InlineData(SessionStatus.Starting)]
    [InlineData(SessionStatus.Running)]
    [InlineData(SessionStatus.Reconnecting)]
    public async Task A_live_session_holds_the_single_slot(SessionStatus status)
    {
        // SPEC §5: one session at a time. The coordinator refuses the second start anyway; the
        // preflight is what lets the UI grey the button out instead of offering a click that throws.
        var monitor = new FakeSessionMonitor { Current = Snapshot(status) };

        var preflight = await Evaluate(monitor: monitor, profile: NvencProfile, channel: TestChannel,
            inputFilePath: InputFile, media: ValidMedia);

        Assert.Equal(StartBlockReason.SessionAlreadyActive, preflight.Reason);
        Assert.False(preflight.CanStart);
    }

    [Theory]
    [InlineData(SessionStatus.Stopped)]
    [InlineData(SessionStatus.Failed)]
    public async Task A_terminated_session_frees_the_slot(SessionStatus status)
    {
        // The session is still there — the UI keeps displaying it — but it is over.
        var monitor = new FakeSessionMonitor { Current = Snapshot(status) };

        var preflight = await Evaluate(monitor: monitor, profile: NvencProfile, channel: TestChannel,
            inputFilePath: InputFile, media: ValidMedia);

        Assert.True(preflight.CanStart);
    }

    // -------------------------------------------------------------------- selection

    [Fact]
    public async Task No_profile_blocks_the_start()
    {
        var preflight = await Evaluate(profile: null, channel: TestChannel, inputFilePath: InputFile, media: ValidMedia);

        Assert.Equal(StartBlockReason.ProfileNotSelected, preflight.Reason);
    }

    [Fact]
    public async Task No_channel_blocks_the_start()
    {
        var preflight = await Evaluate(profile: NvencProfile, channel: null, inputFilePath: InputFile, media: ValidMedia);

        Assert.Equal(StartBlockReason.ChannelNotSelected, preflight.Reason);
    }

    [Fact]
    public async Task No_input_file_blocks_the_start()
    {
        var preflight = await Evaluate(profile: NvencProfile, channel: TestChannel, inputFilePath: null);

        Assert.Equal(StartBlockReason.InputFileNotSelected, preflight.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_input_file_counts_as_none(string path)
    {
        var preflight = await Evaluate(profile: NvencProfile, channel: TestChannel, inputFilePath: path);

        Assert.Equal(StartBlockReason.InputFileNotSelected, preflight.Reason);
    }

    // ------------------------------------------------------------------- media file

    [Fact]
    public async Task A_file_chosen_but_not_analysed_yields_no_verdict()
    {
        var preflight = await Evaluate(
            profile: NvencProfile, channel: TestChannel, inputFilePath: InputFile, media: null);

        Assert.Equal(StartBlockReason.NotChecked, preflight.Reason);
        Assert.False(preflight.CanStart);
    }

    [Fact]
    public async Task A_missing_file_blocks_the_start()
    {
        var media = new MediaValidationResult(
            Exists: false, Readable: false, null, null, null, null, null, null, Error: "File not found.");

        var preflight = await Evaluate(
            profile: NvencProfile, channel: TestChannel, inputFilePath: InputFile, media: media);

        Assert.Equal(StartBlockReason.InputFileNotFound, preflight.Reason);
    }

    [Fact]
    public async Task A_file_ffprobe_cannot_read_blocks_the_start()
    {
        var media = new MediaValidationResult(
            Exists: true, Readable: false, null, null, null, null, null, null, Error: null);

        var preflight = await Evaluate(
            profile: NvencProfile, channel: TestChannel, inputFilePath: InputFile, media: media);

        Assert.Equal(StartBlockReason.InputFileUnreadable, preflight.Reason);
    }

    /// <summary>ffprobe reports a decode failure either by Readable=false OR by an Error: both disqualify.</summary>
    [Fact]
    public async Task A_file_ffprobe_reported_an_error_on_blocks_the_start()
    {
        var media = new MediaValidationResult(
            Exists: true, Readable: true, null, null, null, null, null, null, Error: "Invalid data found.");

        var preflight = await Evaluate(
            profile: NvencProfile, channel: TestChannel, inputFilePath: InputFile, media: media);

        Assert.Equal(StartBlockReason.InputFileUnreadable, preflight.Reason);
    }

    /// <summary>
    /// The ordering rule, and the bug it prevents: picking a corrupt file BEFORE choosing a channel
    /// must still say the file is corrupt. Rank "no channel" first and that message never appears.
    /// </summary>
    [Fact]
    public async Task A_corrupt_file_is_reported_even_before_a_channel_is_chosen()
    {
        var media = new MediaValidationResult(
            Exists: true, Readable: false, null, null, null, null, null, null, Error: null);

        var preflight = await Evaluate(
            profile: null, channel: null, inputFilePath: InputFile, media: media);

        Assert.Equal(StartBlockReason.InputFileUnreadable, preflight.Reason);
    }

    // ----------------------------------------------------------------------- nominal

    [Fact]
    public async Task Everything_in_place_clears_the_start()
    {
        var preflight = await Evaluate(
            profile: NvencProfile, channel: TestChannel, inputFilePath: InputFile, media: ValidMedia);

        Assert.Equal(StartBlockReason.None, preflight.Reason);
        Assert.True(preflight.CanStart);
    }

    // ------------------------------------------------------------------------ fixtures

    private const string InputFile = @"C:\videos\input.mp4";

    private static readonly StreamProfileDto NvencProfile = TestProfile("1080p NVENC", VideoCodec.H264Nvenc, "p2");
    private static readonly StreamProfileDto Libx264Profile = TestProfile("1080p CPU", VideoCodec.Libx264, "veryfast");

    private static readonly ChannelDto TestChannel = new(
        ChannelId.New(), "Twitch principal", Platform.Twitch, "rtmp://live.twitch.tv/app", KeyConfigured: true);

    private static readonly MediaValidationResult ValidMedia = new(
        Exists: true, Readable: true, Duration: TimeSpan.FromMinutes(3),
        Width: 1920, Height: 1080, Fps: 30, VideoCodec: "h264", AudioCodec: "aac", Error: null);

    private static StreamProfileDto TestProfile(string name, VideoCodec codec, string preset)
        => StreamProfileDto.From(StreamProfile.Create(
            name,
            new EncodingSettings(codec, preset, RateControl.Cbr, 3000, 3000, 3000, 60, 60, null, null),
            new AudioSettings(AudioCodec.Aac, 128, 48000),
            InputOptions.Default));

    private static FfmpegEnvironmentReport EnvironmentReport(bool ffmpeg = true, bool ffprobe = true, bool nvenc = true)
        => new(ffmpeg, ffprobe, ffmpeg ? "7.1" : null, nvenc, ffmpeg ? null : "ffmpeg not found.");

    private static SessionSnapshot Snapshot(SessionStatus status)
        => new(SessionId.New(), status, Stats: null, HealthIndicator.Ok, ReconnectAttempts: 0, LastError: null);

    /// <summary>Healthy environment, empty selection, no session — each test overrides what it is about.</summary>
    private static Task<StartPreflight> Evaluate(
        FfmpegEnvironmentReport? environment = null,
        StreamProfileDto? profile = null,
        ChannelDto? channel = null,
        string? inputFilePath = null,
        MediaValidationResult? media = null,
        FakeSessionMonitor? monitor = null)
    {
        var handler = new GetStartPreflightHandler(monitor ?? new FakeSessionMonitor());

        return handler.Handle(new GetStartPreflightQuery(
            environment ?? EnvironmentReport(),
            profile,
            channel,
            inputFilePath,
            media));
    }
}
