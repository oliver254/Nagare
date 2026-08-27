using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nagare.Application.Abstractions;

namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// Seeds <see cref="IFfmpegPaths"/> from <c>settings.json</c> at startup (ADR-0010).
///
/// <para>What makes this safe is not its rank among the hosted services — no other one reads a path
/// when it starts, and the adapters that launch a binary read <see cref="IFfmpegPaths.Current"/> at
/// the moment they use it. It is the composition root: the window is created only once
/// <c>_host.StartAsync()</c> has completed (see App.OnLaunched), so no screen exists, let alone
/// reads the paths, before they are the user's. Being registered ahead of the coordinator remains a
/// cheap precaution for the day a hosted service probes on startup.</para>
///
/// <para>Nothing stored — no file, an empty one, an unreadable one — simply leaves the defaults
/// from appsettings.json in place.</para>
/// </summary>
public sealed class FfmpegSettingsInitializer(
    IFfmpegSettingsStore store,
    IFfmpegPaths paths,
    ILogger<FfmpegSettingsInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await store.LoadAsync(cancellationToken);
            if (settings is null)
            {
                logger.LogInformation(
                    "No user ffmpeg configuration in {SettingsFilePath}; keeping the shipped defaults.",
                    store.SettingsFilePath);
                return;
            }

            paths.Apply(settings);
        }
        catch (Exception ex)
        {
            // A hosted service that throws in StartAsync aborts the host — the window would never
            // open. Refusing to start over a configuration file is exactly backwards: that window
            // is where the file gets fixed.
            logger.LogError(ex, "Could not read {SettingsFilePath}; keeping the shipped defaults.",
                store.SettingsFilePath);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
