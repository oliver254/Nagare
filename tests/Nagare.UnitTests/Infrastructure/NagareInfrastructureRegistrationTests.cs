using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nagare.Application;
using Nagare.Application.Abstractions;
using Nagare.Application.Streaming;
using Nagare.Infrastructure;
using Nagare.Infrastructure.Ffmpeg;

namespace Nagare.UnitTests.Infrastructure;

/// <summary>
/// The wiring itself, because two of its properties are invisible at compile time and would only
/// fail at launch: the container must be able to choose a constructor for the new adapters, and
/// the hosted services must be registered in the intended order.
/// </summary>
public sealed class NagareInfrastructureRegistrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "nagare-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// FfmpegLocator has two public constructors and JsonFfmpegSettingsStore reads its path from
    /// options — neither is a compile-time contract with the container.
    /// </summary>
    [Fact]
    public async Task The_ffmpeg_configuration_services_resolve()
    {
        await using var provider = BuildProvider();

        Assert.IsType<FfmpegPathProvider>(provider.GetRequiredService<IFfmpegPaths>());
        Assert.NotNull(provider.GetRequiredService<IFfmpegSettingsStore>());
        Assert.IsType<FfmpegLocator>(provider.GetRequiredService<IFfmpegLocator>());
    }

    /// <summary>The three adapters that launch a binary now take IFfmpegPaths (ADR-0010).</summary>
    [Fact]
    public async Task The_binary_launching_adapters_resolve_on_the_new_port()
    {
        await using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IFfmpegEnvironmentProbe>());
        Assert.NotNull(provider.GetRequiredService<IFfprobeService>());
        Assert.NotNull(provider.GetRequiredService<IFfmpegProcessRunnerFactory>());
    }

    [Fact]
    public async Task The_shipped_defaults_reach_the_path_provider()
    {
        await using var provider = BuildProvider(
            ("Nagare:Ffmpeg:ExecutablePath", @"C:\shipped\ffmpeg.exe"),
            ("Nagare:Ffmpeg:FfprobePath", @"C:\shipped\ffprobe.exe"));

        var paths = provider.GetRequiredService<IFfmpegPaths>();

        Assert.Equal(@"C:\shipped\ffmpeg.exe", paths.Current.ExecutablePath);
        Assert.Equal(@"C:\shipped\ffprobe.exe", paths.Current.FfprobePath);
    }

    /// <summary>
    /// Hosted services start in registration order, and this locks the initializer ahead of the
    /// coordinator.
    ///
    /// <para>What it covers: that AddNagareInfrastructure registers the initializer as a hosted
    /// service, and that it comes first when the two extension methods are called in this order.
    /// What it does NOT cover: the order used by the real composition root — App.xaml.cs lives in a
    /// WinUI project the test project cannot reference, so the order asserted here is the one this
    /// test itself chose below. Nothing today depends on that order anyway (the coordinator reads
    /// no path on start, the adapters read IFfmpegPaths.Current at use time); the test is kept for
    /// the day a hosted service does probe on startup, when the order will start to matter.</para>
    /// </summary>
    [Fact]
    public async Task The_settings_initializer_starts_before_the_session_coordinator()
    {
        await using var provider = BuildProvider();

        var hostedServices = provider.GetServices<IHostedService>().ToList();
        var initializer = hostedServices.FindIndex(s => s is FfmpegSettingsInitializer);
        var coordinator = hostedServices.FindIndex(s => s is StreamSessionCoordinator);

        Assert.True(initializer >= 0, "FfmpegSettingsInitializer is not registered as a hosted service.");
        Assert.True(coordinator >= 0, "StreamSessionCoordinator is not registered as a hosted service.");
        Assert.True(initializer < coordinator,
            $"The initializer starts at position {initializer}, the coordinator at {coordinator}.");
    }

    private ServiceProvider BuildProvider(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings
                .Select(s => new KeyValuePair<string, string?>(s.Key, s.Value))
                .Append(new KeyValuePair<string, string?>("Nagare:Storage:RootDirectory", _root)))
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Logging by hand rather than AddLogging(): the test project would otherwise need the
        // logging implementation package for two lines that only have to be resolvable.
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Same order as the composition root, and the order the last test is about.
        services.AddNagareInfrastructure(configuration);
        services.AddNagareApplication();

        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
