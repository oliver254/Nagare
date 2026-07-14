using System.Runtime.Versioning;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
