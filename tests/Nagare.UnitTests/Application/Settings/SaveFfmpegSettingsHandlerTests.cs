using Nagare.Application.Abstractions;
using Nagare.Application.Settings;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.Application.Settings;

/// <summary>
/// The save rule of ADR-0010: probe, then write, then publish — each step a gate for the next.
///
/// <para>What the tests are really about is the two ways this can go wrong silently. Persisting a
/// pair that does not run brings the application back up just as stuck as it went down; publishing
/// a pair that failed to reach the disk leaves memory and file disagreeing until a restart quietly
/// undoes the user's choice. Neither shows up in the returned result — only the call counts of the
/// store and of the provider can tell.</para>
/// </summary>
public sealed class SaveFfmpegSettingsHandlerTests
{
    private const string RealFfmpeg = @"C:\tools\ffmpeg\bin\ffmpeg.exe";
    private const string RealFfprobe = @"C:\tools\ffmpeg\bin\ffprobe.exe";

    // ------------------------------------------------------------------- refusals

    [Fact]
    public async Task An_ffmpeg_that_does_not_run_is_not_saved()
    {
        var store = new FakeFfmpegSettingsStore();
        var paths = new FakeFfmpegPaths(new FfmpegPathSettings(@"C:\old\ffmpeg.exe", @"C:\old\ffprobe.exe"));
        var handler = CreateHandler(store, paths, runnable: [RealFfprobe]);

        var result = await handler.Handle(new SaveFfmpegSettingsCommand(@"C:\nope\ffmpeg.exe", RealFfprobe));

        Assert.False(result.Saved);
        Assert.Equal(0, store.SaveCallCount);
        Assert.Equal(0, paths.ApplyCallCount);
        Assert.Equal(@"C:\old\ffmpeg.exe", paths.Current.ExecutablePath);
    }

    /// <summary>ffprobe is not optional: the media validation of every start goes through it.</summary>
    [Fact]
    public async Task An_ffprobe_that_does_not_run_is_not_saved_either()
    {
        var store = new FakeFfmpegSettingsStore();
        var paths = new FakeFfmpegPaths();
        var handler = CreateHandler(store, paths, runnable: [RealFfmpeg]);

        var result = await handler.Handle(new SaveFfmpegSettingsCommand(RealFfmpeg, @"C:\nope\ffprobe.exe"));

        Assert.False(result.Saved);
        Assert.Equal(0, store.SaveCallCount);
        Assert.Equal(0, paths.ApplyCallCount);
    }

    /// <summary>The refusal must say why, or the screen has nothing to show.</summary>
    [Fact]
    public async Task A_refusal_carries_the_report_that_explains_it()
    {
        var handler = CreateHandler(new FakeFfmpegSettingsStore(), new FakeFfmpegPaths(), runnable: []);

        var result = await handler.Handle(new SaveFfmpegSettingsCommand(@"C:\nope\ffmpeg.exe", @"C:\nope\ffprobe.exe"));

        Assert.False(result.Saved);
        Assert.False(result.Report.FfmpegAvailable);
        Assert.False(result.Report.FfprobeAvailable);
    }

    // ---------------------------------------------------------------- acceptance

    [Fact]
    public async Task A_runnable_pair_is_written_then_published()
    {
        var store = new FakeFfmpegSettingsStore();
        var paths = new FakeFfmpegPaths();
        var handler = CreateHandler(store, paths, runnable: [RealFfmpeg, RealFfprobe]);

        var result = await handler.Handle(new SaveFfmpegSettingsCommand(RealFfmpeg, RealFfprobe));

        Assert.True(result.Saved);
        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal(new FfmpegPathSettings(RealFfmpeg, RealFfprobe), store.Stored);
        Assert.Equal(new FfmpegPathSettings(RealFfmpeg, RealFfprobe), paths.Current);
    }

    /// <summary>
    /// Empty is a legitimate answer — "take it from the PATH" — and the only thing that decides is
    /// whether the binary responds from there.
    /// </summary>
    [Fact]
    public async Task Empty_fields_are_accepted_when_the_PATH_answers()
    {
        var store = new FakeFfmpegSettingsStore();
        var paths = new FakeFfmpegPaths(new FfmpegPathSettings(@"C:\old\ffmpeg.exe", @"C:\old\ffprobe.exe"));
        var handler = CreateHandler(store, paths, runnable: ["ffmpeg", "ffprobe"]);

        var result = await handler.Handle(new SaveFfmpegSettingsCommand(string.Empty, string.Empty));

        Assert.True(result.Saved);
        Assert.Equal(FfmpegPathSettings.Empty, store.Stored);
        Assert.Equal(FfmpegPathSettings.Empty, paths.Current);
    }

    [Fact]
    public async Task Empty_fields_are_refused_when_the_PATH_does_not_answer()
    {
        var store = new FakeFfmpegSettingsStore();
        var handler = CreateHandler(store, new FakeFfmpegPaths(), runnable: [RealFfmpeg, RealFfprobe]);

        var result = await handler.Handle(new SaveFfmpegSettingsCommand(string.Empty, string.Empty));

        Assert.False(result.Saved);
        Assert.Equal(0, store.SaveCallCount);
    }

    /// <summary>A path pasted from Explorer carries a trailing space; tested and saved must match.</summary>
    [Fact]
    public async Task Surrounding_whitespace_never_reaches_the_probe_nor_the_file()
    {
        var store = new FakeFfmpegSettingsStore();
        var probe = new FakeFfmpegEnvironmentProbe(RealFfmpeg, RealFfprobe);
        var handler = new SaveFfmpegSettingsHandler(probe, store, new FakeFfmpegPaths());

        var result = await handler.Handle(new SaveFfmpegSettingsCommand($"  {RealFfmpeg} ", $"\t{RealFfprobe}"));

        Assert.True(result.Saved);
        Assert.Equal(new FfmpegPathSettings(RealFfmpeg, RealFfprobe), probe.LastProbed);
        Assert.Equal(new FfmpegPathSettings(RealFfmpeg, RealFfprobe), store.Stored);
    }

    // ------------------------------------------------------------------- ordering

    /// <summary>
    /// The verdict is about the pair being SUBMITTED, never about the one currently in effect —
    /// otherwise the save would keep approving the configuration it is replacing.
    /// </summary>
    [Fact]
    public async Task The_probe_runs_on_the_submitted_paths_not_on_the_ones_in_effect()
    {
        var probe = new FakeFfmpegEnvironmentProbe(RealFfmpeg, RealFfprobe)
        {
            CurrentPaths = new FfmpegPathSettings(@"C:\old\ffmpeg.exe", @"C:\old\ffprobe.exe")
        };
        var handler = new SaveFfmpegSettingsHandler(probe, new FakeFfmpegSettingsStore(), new FakeFfmpegPaths());

        await handler.Handle(new SaveFfmpegSettingsCommand(RealFfmpeg, RealFfprobe));

        Assert.Equal(new FfmpegPathSettings(RealFfmpeg, RealFfprobe), probe.LastProbed);
    }

    /// <summary>
    /// Apply comes AFTER the write, and only if it succeeded. Publish first and a failed write
    /// leaves the running application on paths the next startup will not remember.
    /// </summary>
    [Fact]
    public async Task A_write_that_fails_leaves_the_paths_in_effect_untouched()
    {
        var store = new FakeFfmpegSettingsStore { SaveFailure = new IOException("Disk is full.") };
        var paths = new FakeFfmpegPaths(new FfmpegPathSettings(@"C:\old\ffmpeg.exe", @"C:\old\ffprobe.exe"));
        var handler = CreateHandler(store, paths, runnable: [RealFfmpeg, RealFfprobe]);

        await Assert.ThrowsAsync<IOException>(
            () => handler.Handle(new SaveFfmpegSettingsCommand(RealFfmpeg, RealFfprobe)));

        Assert.Equal(0, paths.ApplyCallCount);
        Assert.Equal(@"C:\old\ffmpeg.exe", paths.Current.ExecutablePath);
    }

    private static SaveFfmpegSettingsHandler CreateHandler(
        FakeFfmpegSettingsStore store,
        FakeFfmpegPaths paths,
        params string[] runnable)
        => new(new FakeFfmpegEnvironmentProbe(runnable), store, paths);
}
