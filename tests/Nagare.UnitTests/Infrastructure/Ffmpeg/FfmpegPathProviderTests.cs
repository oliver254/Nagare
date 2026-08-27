using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;
using Nagare.Infrastructure.Ffmpeg;

namespace Nagare.UnitTests.Infrastructure.Ffmpeg;

/// <summary>
/// The precedence rule of ADR-0010, in the one place that holds it: the shipped default until the
/// user says otherwise, the user's choice from then on — and visible AT ONCE, not after a reload.
/// </summary>
public sealed class FfmpegPathProviderTests
{
    [Fact]
    public void Before_anything_is_applied_the_shipped_defaults_are_in_effect()
    {
        var provider = CreateProvider(@"C:\shipped\ffmpeg.exe", @"C:\shipped\ffprobe.exe");

        Assert.Equal(@"C:\shipped\ffmpeg.exe", provider.Current.ExecutablePath);
        Assert.Equal(@"C:\shipped\ffprobe.exe", provider.Current.FfprobePath);
    }

    /// <summary>Empty defaults are the shipped appsettings.json of a machine that has ffmpeg on the PATH.</summary>
    [Fact]
    public void Empty_defaults_resolve_to_the_binaries_on_the_PATH()
    {
        var provider = CreateProvider(string.Empty, string.Empty);

        Assert.Equal("ffmpeg", provider.Current.ResolvedFfmpeg);
        Assert.Equal("ffprobe", provider.Current.ResolvedFfprobe);
    }

    [Fact]
    public void An_applied_configuration_outranks_the_shipped_defaults()
    {
        var provider = CreateProvider(@"C:\shipped\ffmpeg.exe", @"C:\shipped\ffprobe.exe");

        provider.Apply(new FfmpegPathSettings(@"D:\user\ffmpeg.exe", @"D:\user\ffprobe.exe"));

        Assert.Equal(@"D:\user\ffmpeg.exe", provider.Current.ExecutablePath);
        Assert.Equal(@"D:\user\ffprobe.exe", provider.Current.FfprobePath);
    }

    /// <summary>
    /// The reason this is a provider and not an IOptionsMonitor over a watched file (ADR-0010,
    /// alternatives): the read that follows the write sees the write. No delay, no polling.
    /// </summary>
    [Fact]
    public void The_new_paths_are_readable_on_the_line_after_Apply()
    {
        var provider = CreateProvider(@"C:\shipped\ffmpeg.exe", @"C:\shipped\ffprobe.exe");

        provider.Apply(new FfmpegPathSettings(@"D:\user\ffmpeg.exe", @"D:\user\ffprobe.exe"));
        var immediately = provider.Current;

        Assert.Equal(@"D:\user\ffmpeg.exe", immediately.ExecutablePath);
    }

    [Fact]
    public void Applying_twice_keeps_the_last_word()
    {
        var provider = CreateProvider(@"C:\shipped\ffmpeg.exe", @"C:\shipped\ffprobe.exe");

        provider.Apply(new FfmpegPathSettings(@"D:\first\ffmpeg.exe", @"D:\first\ffprobe.exe"));
        provider.Apply(new FfmpegPathSettings(@"D:\second\ffmpeg.exe", @"D:\second\ffprobe.exe"));

        Assert.Equal(@"D:\second\ffmpeg.exe", provider.Current.ExecutablePath);
    }

    private static FfmpegPathProvider CreateProvider(string executablePath, string ffprobePath)
        => new(Options.Create(new FfmpegOptions { ExecutablePath = executablePath, FfprobePath = ffprobePath }));
}
