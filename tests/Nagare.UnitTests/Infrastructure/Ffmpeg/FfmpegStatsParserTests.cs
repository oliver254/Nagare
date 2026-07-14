using Nagare.Infrastructure.Ffmpeg;

namespace Nagare.UnitTests.Infrastructure.Ffmpeg;

/// <summary>
/// Pure parser of the ffmpeg progression lines (ARCHITECTURE.md §6.2). The class is
/// internal but unit-tested through InternalsVisibleTo (§8.4).
/// </summary>
public sealed class FfmpegStatsParserTests
{
    [Fact]
    public void TryParse_FullProgressionLine_ExtractsEveryField()
    {
        const string line =
            "frame= 1234 fps= 30 q=28.0 size=    2048kB time=00:00:41.20 bitrate=3000.1kbits/s dup=2 drop=5 speed=1.01x";

        var stats = FfmpegStatsParser.TryParse(line);

        Assert.NotNull(stats);
        Assert.Equal(1234, stats.Frame);
        Assert.Equal(30d, stats.Fps, 3);
        Assert.Equal(3000.1d, stats.BitrateKbps, 3);
        Assert.Equal(1.01d, stats.Speed, 3);
        Assert.Equal(5, stats.DroppedFrames);
        Assert.Equal(2, stats.DupFrames);
        Assert.Equal(41.20d, stats.Time.TotalSeconds, 3);
    }

    [Fact]
    public void TryParse_LineWithHoursAndMinutes_ComputesTheFullElapsedTime()
    {
        const string line =
            "frame=180000 fps= 60 q=-1.0 Lsize=  512000kB time=01:23:45.67 bitrate=6000.0kbits/s dup=0 drop=12 speed=0.99x";

        var stats = FfmpegStatsParser.TryParse(line);

        Assert.NotNull(stats);
        Assert.Equal(180000, stats.Frame);
        Assert.Equal((3600 + (23 * 60) + 45.67), stats.Time.TotalSeconds, 3);
        Assert.Equal(12, stats.DroppedFrames);
    }

    [Fact]
    public void TryParse_LineWithoutDropAndDup_DefaultsThemToZero()
    {
        const string line =
            "frame=  120 fps= 30 q=28.0 size=    1024kB time=00:00:04.00 bitrate=3000.0kbits/s speed=1.0x";

        var stats = FfmpegStatsParser.TryParse(line);

        Assert.NotNull(stats);
        Assert.Equal(0, stats.DroppedFrames);
        Assert.Equal(0, stats.DupFrames);
    }

    [Fact]
    public void TryParse_LineWithUnavailableBitrate_YieldsZeroInsteadOfThrowing()
    {
        // ffmpeg prints "bitrate=N/A" on the very first frames.
        const string line =
            "frame=    1 fps=0.0 q=0.0 size=       0kB time=00:00:00.04 bitrate=N/A speed=0.0757x";

        var stats = FfmpegStatsParser.TryParse(line);

        Assert.NotNull(stats);
        Assert.Equal(1, stats.Frame);
        Assert.Equal(0d, stats.BitrateKbps, 3);
        Assert.Equal(0.0757d, stats.Speed, 4);
    }

    [Fact]
    public void TryParse_SlowSpeedLine_ReportsSpeedBelowRealTime()
    {
        // Health indicator input: speed < 1.0x means ffmpeg cannot keep up (SPEC §6).
        const string line =
            "frame= 600 fps= 25 q=30.0 size=   8192kB time=00:00:20.00 bitrate=3355.4kbits/s dup=0 drop=41 speed=0.85x";

        var stats = FfmpegStatsParser.TryParse(line);

        Assert.NotNull(stats);
        Assert.Equal(0.85d, stats.Speed, 3);
        Assert.Equal(41, stats.DroppedFrames);
    }

    [Theory]
    [InlineData("ffmpeg version 7.1 Copyright (c) 2000-2024 the FFmpeg developers")]
    [InlineData("  Stream #0:0(und): Video: h264 (High) (avc1 / 0x31637661), yuv420p, 1920x1080, 30 fps")]
    [InlineData("[rtmp @ 000001c] rtmp://live.twitch.tv/app/****: Connection refused")]
    [InlineData("Press [q] to stop, [?] for help")]
    [InlineData("Output #0, flv, to 'rtmp://live.twitch.tv/app/****':")]
    public void TryParse_NonStatsLine_ReturnsNull(string line)
        => Assert.Null(FfmpegStatsParser.TryParse(line));

    [Fact]
    public void TryParse_LineWithFrameButNoTime_ReturnsNull()
    {
        // A progression line always carries both frame= and time=; anything else is a log line.
        Assert.Null(FfmpegStatsParser.TryParse("frame= 1234 (this is not a progression line)"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_BlankLine_ReturnsNull(string? line)
        => Assert.Null(FfmpegStatsParser.TryParse(line));
}
