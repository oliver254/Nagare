using Nagare.Application.Abstractions;
using Nagare.Domain.Channels;
using Nagare.Domain.Profiles;
using Nagare.Infrastructure.Ffmpeg;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.Infrastructure.Ffmpeg;

/// <summary>
/// Golden tests of the STRICT canonical argument order (ARCHITECTURE.md §6.1) and of the
/// non-leak of the stream key in the displayable command line (SPEC §4, ADR-0005).
/// </summary>
public sealed class FfmpegCommandBuilderTests
{
    private const string InputFile = "in.mp4";
    private const string TwitchBaseUrl = "rtmp://live.twitch.tv/app";
    private const string StreamKey = "live_2468_KpH2sAbCdEf";

    /// <summary>The exact command line required by the spec, character for character.</summary>
    private const string SpecCommandLine =
        "-re -stream_loop -1 -i in.mp4 -c:v h264_nvenc -preset p2 -rc cbr " +
        "-b:v 3000k -maxrate 3000k -bufsize 3000k -g 60 -keyint_min 60 " +
        "-c:a aac -b:a 128k -ar 48000 -f flv rtmp://live.twitch.tv/app/live_2468_KpH2sAbCdEf";

    private readonly FakeStreamKeyProtector _protector = new();

    // ---------------------------------------------------------------- golden test

    [Fact]
    public void Build_SpecCbrNvencProfile_ProducesExactSpecCommandLine()
    {
        var command = Build(SpecProfile(), TwitchChannel());

        Assert.Equal(SpecCommandLine, string.Join(' ', command.Arguments));
    }

    [Fact]
    public void Build_SpecCbrNvencProfile_MasksOnlyTheKeyInTheDisplayableLine()
    {
        var command = Build(SpecProfile(), TwitchChannel());

        Assert.Equal(
            SpecCommandLine.Replace(StreamKey, ProtectedStreamKey.Mask, StringComparison.Ordinal),
            command.MaskedCommandLine);
    }

    // ------------------------------------------------------- key must never leak

    [Fact]
    public void Build_AnyProfile_MaskedCommandLineNeverContainsThePlaintextKey()
    {
        var command = Build(SpecProfile(), TwitchChannel());

        Assert.DoesNotContain(StreamKey, command.MaskedCommandLine, StringComparison.Ordinal);
        Assert.EndsWith($"/{ProtectedStreamKey.Mask}", command.MaskedCommandLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_OfBuiltCommand_ReturnsTheMaskedLineNotTheRealArguments()
    {
        var command = Build(SpecProfile(), TwitchChannel());

        // An accidental log / string interpolation of the command must be harmless.
        Assert.Equal(command.MaskedCommandLine, command.ToString());
        Assert.DoesNotContain(StreamKey, $"{command}", StringComparison.Ordinal);
    }

    [Fact]
    public void Build_AnyProfile_ExposesThePlaintextKeyOnlyInArgumentsAndSecrets()
    {
        var command = Build(SpecProfile(), TwitchChannel());

        Assert.Equal([StreamKey], command.Secrets);
        Assert.Equal($"{TwitchBaseUrl}/{StreamKey}", command.Arguments[^1]);
    }

    [Fact]
    public void Build_AnyProfile_DecryptsTheKeyExactlyOnce()
    {
        Build(SpecProfile(), TwitchChannel());

        Assert.Equal(1, _protector.UnprotectCallCount);
    }

    // ------------------------------------------------------------------ variants

    [Fact]
    public void Build_Libx264Profile_OmitsTheRateControlArgument()
    {
        // libx264 has no -rc: its CBR is expressed by b:v = maxrate + bufsize (§6.1, row 6).
        var video = new EncodingSettings(
            VideoCodec.Libx264, "veryfast", RateControl.Cbr,
            bitrateKbps: 4500, maxrateKbps: 4500, bufsizeKbps: 9000,
            gopSize: 120, keyintMin: 120, resolution: null, fps: null);

        var command = Build(ProfileWith(video), TwitchChannel());

        Assert.Equal(
            "-re -stream_loop -1 -i in.mp4 -c:v libx264 -preset veryfast "
            + "-b:v 4500k -maxrate 4500k -bufsize 9000k -g 120 -keyint_min 120 "
            + $"-c:a aac -b:a 128k -ar 48000 -f flv {TwitchBaseUrl}/{StreamKey}",
            string.Join(' ', command.Arguments));
    }

    [Fact]
    public void Build_VbrNvencProfile_EmitsRcVbrAndTheHigherMaxrate()
    {
        var video = new EncodingSettings(
            VideoCodec.H264Nvenc, "p4", RateControl.Vbr,
            bitrateKbps: 6000, maxrateKbps: 9000, bufsizeKbps: 12000,
            gopSize: 60, keyintMin: 30, resolution: null, fps: null);

        var command = Build(ProfileWith(video), TwitchChannel());

        Assert.Equal(
            "-re -stream_loop -1 -i in.mp4 -c:v h264_nvenc -preset p4 -rc vbr "
            + "-b:v 6000k -maxrate 9000k -bufsize 12000k -g 60 -keyint_min 30 "
            + $"-c:a aac -b:a 128k -ar 48000 -f flv {TwitchBaseUrl}/{StreamKey}",
            string.Join(' ', command.Arguments));
    }

    [Fact]
    public void Build_HevcNvencProfile_EmitsHevcCodecAndKeepsRateControl()
    {
        var video = new EncodingSettings(
            VideoCodec.HevcNvenc, "p7", RateControl.Cbr,
            bitrateKbps: 8000, maxrateKbps: 8000, bufsizeKbps: 16000,
            gopSize: 50, keyintMin: 25, resolution: null, fps: null);

        var command = Build(ProfileWith(video), TwitchChannel());

        Assert.Contains("-c:v hevc_nvenc -preset p7 -rc cbr", string.Join(' ', command.Arguments), StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ProfileWithResolutionAndFps_InsertsScaleAndRateBetweenKeyintAndAudio()
    {
        var video = new EncodingSettings(
            VideoCodec.H264Nvenc, "p2", RateControl.Cbr,
            bitrateKbps: 3000, maxrateKbps: 3000, bufsizeKbps: 3000,
            gopSize: 60, keyintMin: 60,
            resolution: new Resolution(1280, 720), fps: 30);

        var command = Build(ProfileWith(video), TwitchChannel());

        Assert.Equal(
            "-re -stream_loop -1 -i in.mp4 -c:v h264_nvenc -preset p2 -rc cbr "
            + "-b:v 3000k -maxrate 3000k -bufsize 3000k -g 60 -keyint_min 60 "
            + "-vf scale=1280:720 -r 30 "
            + $"-c:a aac -b:a 128k -ar 48000 -f flv {TwitchBaseUrl}/{StreamKey}",
            string.Join(' ', command.Arguments));
    }

    [Fact]
    public void Build_InputOptionsAllDisabled_OmitsReAndStreamLoop()
    {
        var profile = StreamProfile.Create(
            "No loop",
            SpecVideo(),
            SpecAudio(),
            new InputOptions(ReadAtNativeRate: false, LoopInfinitely: false));

        var command = Build(profile, TwitchChannel());

        Assert.StartsWith("-i in.mp4 -c:v h264_nvenc", string.Join(' ', command.Arguments), StringComparison.Ordinal);
        Assert.DoesNotContain("-re", command.Arguments);
        Assert.DoesNotContain("-stream_loop", command.Arguments);
    }

    [Theory]
    [InlineData("rtmp://live.twitch.tv/app")]
    [InlineData("rtmp://live.twitch.tv/app/")]
    [InlineData("rtmp://live.twitch.tv/app///")]
    public void Build_BaseUrlWithOrWithoutTrailingSlash_ProducesASingleSeparator(string baseUrl)
    {
        var command = Build(SpecProfile(), TwitchChannel(baseUrl));

        Assert.Equal($"rtmp://live.twitch.tv/app/{StreamKey}", command.Arguments[^1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_BlankInputFilePath_ThrowsArgumentException(string inputFilePath)
        => Assert.Throws<ArgumentException>(
            () => new FfmpegCommandBuilder(_protector).Build(SpecProfile(), TwitchChannel(), inputFilePath));

    // ------------------------------------------------------------------- helpers

    private FfmpegCommand Build(StreamProfile profile, Channel channel)
        => new FfmpegCommandBuilder(_protector).Build(profile, channel, InputFile);

    private Channel TwitchChannel(string baseUrl = TwitchBaseUrl)
        => Channel.Create("Twitch", Platform.Twitch, baseUrl, _protector.Protect(StreamKey));

    private static StreamProfile SpecProfile() => ProfileWith(SpecVideo());

    private static StreamProfile ProfileWith(EncodingSettings video)
        => StreamProfile.Create("Spec", video, SpecAudio(), InputOptions.Default);

    /// <summary>h264_nvenc, p2, CBR 3000/3000/3000, g=60, keyint_min=60, no scale, no fps.</summary>
    private static EncodingSettings SpecVideo() => new(
        VideoCodec.H264Nvenc, "p2", RateControl.Cbr,
        bitrateKbps: 3000, maxrateKbps: 3000, bufsizeKbps: 3000,
        gopSize: 60, keyintMin: 60, resolution: null, fps: null);

    private static AudioSettings SpecAudio() => new(AudioCodec.Aac, bitrateKbps: 128, sampleRateHz: 48000);
}
