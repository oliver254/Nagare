using Nagare.Application.Abstractions;
using Nagare.Infrastructure.Ffmpeg;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.Infrastructure.Ffmpeg;

/// <summary>
/// The probe launches real processes, so the suite must not depend on ffmpeg being installed on
/// the machine running it. A stock Windows binary stands in: what is asserted here is never what
/// the binary answers, only what the probe DOES with it — it comes back, or it gives up.
/// </summary>
public sealed class FfmpegEnvironmentProbeTests
{
    /// <summary>
    /// Stand-in for ffmpeg. Present on every Windows install, answers an unknown option at once,
    /// touches neither the network nor stdin — so "a process really started and really ended" is a
    /// fact of the test rather than of what happens to be installed.
    /// </summary>
    private static readonly string StockBinary = Path.Combine(Environment.SystemDirectory, "ping.exe");

    /// <summary>
    /// The control the next test needs: the SAME binary, given the normal deadline, is reported
    /// available. Without it, "unavailable under a spent deadline" would also pass if the probe had
    /// simply become unable to launch anything.
    /// </summary>
    [Fact]
    public async Task A_binary_that_answers_within_the_deadline_is_reported_available()
    {
        var paths = new FakeFfmpegPaths(new FfmpegPathSettings(StockBinary, StockBinary));

        // The overload without arguments, which probes the paths in effect (ADR-0010).
        var report = await new FfmpegEnvironmentProbe(paths).CheckAsync(CancellationToken.None);

        Assert.True(report.FfmpegAvailable);
        Assert.True(report.FfprobeAvailable);
        Assert.Null(report.Error);
        Assert.False(report.NvencAvailable);   // nothing in that output names h264_nvenc
    }

    /// <summary>
    /// The defect this guards against: a binary that never gives the hand back used to freeze
    /// "Test" — and "Save", which probes first — for as long as the user did not close the window
    /// it had opened. The file picker only filters on ".exe", so designating one is a slip away.
    ///
    /// <para>The deadline is injected as ZERO rather than as a few milliseconds, and that is
    /// deliberate: it is armed before the process is even started, so it has an entire process
    /// launch to fire in, while a small positive delay would be racing the ~20 ms lifetime of the
    /// stand-in binary — and a test that depends on which of two clocks wins is worse than no test.
    /// A binary that genuinely hangs would express the intent better, but no stock Windows binary
    /// does so on a fixed <c>-version</c> argument, and none that depends on how the suite was
    /// launched (an inherited stdin) would be reliable.</para>
    /// </summary>
    [Fact]
    public async Task A_binary_that_outlives_the_deadline_is_reported_unavailable()
    {
        var probe = new FfmpegEnvironmentProbe(new FakeFfmpegPaths(), launchTimeout: TimeSpan.Zero);

        var report = await probe.CheckAsync(new FfmpegPathSettings(StockBinary, StockBinary), CancellationToken.None);

        Assert.False(report.FfmpegAvailable);
        Assert.False(report.FfprobeAvailable);
        Assert.Null(report.FfmpegVersion);
        Assert.False(report.NvencAvailable);
        Assert.NotNull(report.Error);
    }

    /// <summary>
    /// The path that already existed, kept under assertion: giving up on a deadline must not have
    /// changed what an absent binary reports.
    /// </summary>
    [Fact]
    public async Task A_binary_that_does_not_exist_is_reported_unavailable()
    {
        var missing = Path.Combine(Path.GetTempPath(), "nagare-tests", $"{Guid.NewGuid():N}.exe");

        var report = await new FfmpegEnvironmentProbe(new FakeFfmpegPaths())
            .CheckAsync(new FfmpegPathSettings(missing, missing), CancellationToken.None);

        Assert.False(report.FfmpegAvailable);
        Assert.False(report.FfprobeAvailable);
        Assert.NotNull(report.Error);
    }
}
