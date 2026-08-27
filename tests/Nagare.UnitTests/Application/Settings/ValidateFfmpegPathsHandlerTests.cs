using Nagare.Application.Abstractions;
using Nagare.Application.Settings;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.Application.Settings;

/// <summary>
/// The "Test" button. Its whole value is that it answers for the pair the user is about to save —
/// a check on the paths in effect would keep approving the configuration being replaced, and would
/// be useless on the very screen that exists because the current one does not work.
/// </summary>
public sealed class ValidateFfmpegPathsHandlerTests
{
    private const string RealFfmpeg = @"C:\tools\ffmpeg\bin\ffmpeg.exe";
    private const string RealFfprobe = @"C:\tools\ffmpeg\bin\ffprobe.exe";

    [Fact]
    public async Task The_paths_given_are_the_paths_probed()
    {
        var probe = new FakeFfmpegEnvironmentProbe(RealFfmpeg, RealFfprobe)
        {
            CurrentPaths = new FfmpegPathSettings(@"C:\in-effect\ffmpeg.exe", @"C:\in-effect\ffprobe.exe")
        };

        var report = await new ValidateFfmpegPathsHandler(probe)
            .Handle(new ValidateFfmpegPathsQuery(RealFfmpeg, RealFfprobe));

        Assert.Equal(new FfmpegPathSettings(RealFfmpeg, RealFfprobe), probe.LastProbed);
        Assert.True(report.FfmpegAvailable);
        Assert.True(report.FfprobeAvailable);
    }

    /// <summary>Testing an empty field must exercise the PATH, not be short-circuited as "nothing to test".</summary>
    [Fact]
    public async Task Empty_fields_are_probed_against_the_PATH()
    {
        var probe = new FakeFfmpegEnvironmentProbe("ffmpeg", "ffprobe");

        var report = await new ValidateFfmpegPathsHandler(probe)
            .Handle(new ValidateFfmpegPathsQuery(string.Empty, string.Empty));

        Assert.True(report.FfmpegAvailable);
        Assert.True(report.FfprobeAvailable);
    }

    /// <summary>Testing must not change anything: it is a question, not a decision.</summary>
    [Fact]
    public async Task A_failing_test_reports_without_changing_anything()
    {
        var probe = new FakeFfmpegEnvironmentProbe(RealFfprobe);

        var report = await new ValidateFfmpegPathsHandler(probe)
            .Handle(new ValidateFfmpegPathsQuery(@"C:\nope\ffmpeg.exe", RealFfprobe));

        Assert.False(report.FfmpegAvailable);
        Assert.True(report.FfprobeAvailable);
    }
}
