using System.Runtime.Versioning;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;
using Nagare.Infrastructure.Ffmpeg;
using Nagare.Infrastructure.Persistence;
using Nagare.Infrastructure.Security;

namespace Nagare.Infrastructure;

/// <summary>
/// Explicit registration of the Infrastructure layer (ARCHITECTURE.md §6). Wires Data
/// Protection (DPAPI keyring under %APPDATA%\Nagare\keys), JSON persistence and the ffmpeg
/// adapters.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNagareInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<NagareStorageOptions>().BindConfiguration(NagareStorageOptions.SectionName);
        services.AddOptions<FfmpegOptions>().BindConfiguration(FfmpegOptions.SectionName);

        ConfigureDataProtection(services);

        // Security
        services.AddSingleton<IStreamKeyProtector, DataProtectionStreamKeyProtector>();

        // Persistence
        services.AddSingleton<IStreamProfileRepository, JsonStreamProfileRepository>();
        services.AddSingleton<IChannelRepository, JsonChannelRepository>();
        services.AddSingleton<IFfmpegSettingsStore, JsonFfmpegSettingsStore>();

        // ffmpeg paths (ADR-0010): the runtime truth, its detection, and the hosted service that
        // seeds it from settings.json. Registered before AddNagareApplication and its
        // StreamSessionCoordinator, so the initializer starts first — a cheap precaution rather
        // than a dependency: the coordinator reads no path when it starts, and the adapters that
        // launch a binary read IFfmpegPaths.Current when they use it. What guarantees the screens
        // never see unseeded paths is the composition root awaiting host startup before creating
        // the window (App.OnLaunched).
        services.AddSingleton<IFfmpegPaths, FfmpegPathProvider>();
        services.AddSingleton<IFfmpegLocator, FfmpegLocator>();
        services.AddSingleton<IHostedService, FfmpegSettingsInitializer>();

        // ffmpeg / ffprobe
        services.AddSingleton<IFfmpegCommandBuilder, FfmpegCommandBuilder>();
        services.AddSingleton<IFfmpegProcessRunnerFactory, FfmpegProcessRunnerFactory>();
        services.AddSingleton<IFfprobeService, FfprobeService>();
        services.AddSingleton<IFfmpegEnvironmentProbe, FfmpegEnvironmentProbe>();

        return services;
    }

    private static void ConfigureDataProtection(IServiceCollection services)
    {
        // The keyring path depends on resolved storage options.
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
        {
            var storage = sp.GetRequiredService<IOptions<NagareStorageOptions>>().Value;
            var keyDir = new DirectoryInfo(storage.KeyringDirectory);
            keyDir.Create();

            return new ConfigureNamedOptions<KeyManagementOptions>(Options.DefaultName, options =>
            {
                options.XmlRepository = new Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository(
                    keyDir,
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
            });
        });

        var builder = services.AddDataProtection().SetApplicationName("Nagare");

        if (OperatingSystem.IsWindows())
            ProtectKeyringWithDpapi(builder);
    }

    [SupportedOSPlatform("windows")]
    private static void ProtectKeyringWithDpapi(IDataProtectionBuilder builder)
        => builder.ProtectKeysWithDpapi();
}
