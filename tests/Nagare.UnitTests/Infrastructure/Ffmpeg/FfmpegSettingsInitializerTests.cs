using Nagare.Application.Abstractions;
using Nagare.Infrastructure.Ffmpeg;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.Infrastructure.Ffmpeg;

/// <summary>
/// The startup hop from settings.json to the runtime paths (ADR-0010). Small surface, one rule
/// that matters: it must never be the reason the application does not open.
/// </summary>
public sealed class FfmpegSettingsInitializerTests
{
    [Fact]
    public async Task A_stored_configuration_is_in_effect_once_the_host_has_started()
    {
        var store = new FakeFfmpegSettingsStore
        {
            Stored = new FfmpegPathSettings(@"D:\user\ffmpeg.exe", @"D:\user\ffprobe.exe")
        };
        var paths = new FakeFfmpegPaths(new FfmpegPathSettings(@"C:\shipped\ffmpeg.exe", @"C:\shipped\ffprobe.exe"));

        await CreateInitializer(store, paths).StartAsync(CancellationToken.None);

        Assert.Equal(@"D:\user\ffmpeg.exe", paths.Current.ExecutablePath);
    }

    [Fact]
    public async Task Nothing_stored_leaves_the_shipped_defaults_alone()
    {
        var store = new FakeFfmpegSettingsStore { Stored = null };
        var paths = new FakeFfmpegPaths(new FfmpegPathSettings(@"C:\shipped\ffmpeg.exe", @"C:\shipped\ffprobe.exe"));

        await CreateInitializer(store, paths).StartAsync(CancellationToken.None);

        Assert.Equal(0, paths.ApplyCallCount);
        Assert.Equal(@"C:\shipped\ffmpeg.exe", paths.Current.ExecutablePath);
    }

    /// <summary>
    /// A hosted service that throws in StartAsync aborts the host, and the window never opens —
    /// over a configuration file whose repair screen lives inside that very window.
    /// </summary>
    [Fact]
    public async Task An_unreadable_file_does_not_take_the_startup_down()
    {
        var store = new FakeFfmpegSettingsStore { LoadFailure = new IOException("The file is locked.") };
        var paths = new FakeFfmpegPaths(new FfmpegPathSettings(@"C:\shipped\ffmpeg.exe", @"C:\shipped\ffprobe.exe"));
        var logger = new CapturingLogger<FfmpegSettingsInitializer>();

        await new FfmpegSettingsInitializer(store, paths, logger).StartAsync(CancellationToken.None);

        Assert.Equal(@"C:\shipped\ffmpeg.exe", paths.Current.ExecutablePath);
        Assert.NotEmpty(logger.Messages);
    }

    private static FfmpegSettingsInitializer CreateInitializer(IFfmpegSettingsStore store, IFfmpegPaths paths)
        => new(store, paths, new CapturingLogger<FfmpegSettingsInitializer>());
}
