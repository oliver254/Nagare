using Nagare.Application.Abstractions;
using Nagare.Application.Settings;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.Application.Settings;

/// <summary>
/// What the settings screen reads. The one distinction worth a test: the fields always show the
/// paths IN EFFECT, while IsUserConfigured says whether those paths are a choice or a default.
/// </summary>
public sealed class GetFfmpegSettingsHandlerTests
{
    [Fact]
    public async Task The_view_shows_the_paths_in_effect()
    {
        var paths = new FakeFfmpegPaths(new FfmpegPathSettings(@"D:\user\ffmpeg.exe", @"D:\user\ffprobe.exe"));

        var view = await Handle(paths, new FakeFfmpegSettingsStore());

        Assert.Equal(@"D:\user\ffmpeg.exe", view.ExecutablePath);
        Assert.Equal(@"D:\user\ffprobe.exe", view.FfprobePath);
    }

    /// <summary>The file path is shown so the user can find, back up or delete the file.</summary>
    [Fact]
    public async Task The_view_says_where_the_configuration_lives()
    {
        var store = new FakeFfmpegSettingsStore { SettingsFilePath = @"C:\somewhere\Nagare\settings.json" };

        var view = await Handle(new FakeFfmpegPaths(), store);

        Assert.Equal(@"C:\somewhere\Nagare\settings.json", view.SettingsFilePath);
    }

    [Fact]
    public async Task A_stored_file_makes_the_configuration_the_users()
    {
        var store = new FakeFfmpegSettingsStore
        {
            Stored = new FfmpegPathSettings(@"D:\user\ffmpeg.exe", @"D:\user\ffprobe.exe")
        };

        var view = await Handle(new FakeFfmpegPaths(), store);

        Assert.True(view.IsUserConfigured);
    }

    /// <summary>
    /// Same paths on screen, different meaning: without a stored file they are the defaults shipped
    /// next to the executable, which the next update is free to replace.
    /// </summary>
    [Fact]
    public async Task Without_a_stored_file_the_configuration_is_only_a_default()
    {
        var paths = new FakeFfmpegPaths(new FfmpegPathSettings(@"C:\shipped\ffmpeg.exe", @"C:\shipped\ffprobe.exe"));

        var view = await Handle(paths, new FakeFfmpegSettingsStore { Stored = null });

        Assert.False(view.IsUserConfigured);
        Assert.Equal(@"C:\shipped\ffmpeg.exe", view.ExecutablePath);
    }

    private static Task<FfmpegSettingsView> Handle(IFfmpegPaths paths, IFfmpegSettingsStore store)
        => new GetFfmpegSettingsHandler(paths, store).Handle(new GetFfmpegSettingsQuery());
}
