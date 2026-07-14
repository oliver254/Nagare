using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Monbsoft.BrilliantMediator.Extensions;
using Nagare.Application.Abstractions;
using Nagare.Application.Streaming;

namespace Nagare.Application;

/// <summary>
/// DI registration of the Application layer (ADR-0007). The handlers are no longer listed one by
/// one: <c>AddGeneratedHandlers()</c> is emitted at compile time by BrilliantMediator.SourceGenerator,
/// which scans THIS assembly — so a new handler can no longer be forgotten in the wiring.
///
/// The composition root must still call <c>UseBrilliantMediator()</c> on the built
/// <see cref="IServiceProvider"/>: it is what populates the dispatch registry.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNagareApplication(this IServiceCollection services)
    {
        services.AddOptions<ReconnectSettings>()
            .BindConfiguration(ReconnectSettings.SectionName);

        // Source of the reconnection backoff delay (ADR-0008). The BCL abstraction, so the
        // coordinator can be tested without waiting for real seconds.
        services.TryAddSingleton(TimeProvider.System);

        // The coordinator is a single shared instance exposing three roles.
        services.AddSingleton<StreamSessionCoordinator>();
        services.AddSingleton<IStreamSessionCoordinator>(sp => sp.GetRequiredService<StreamSessionCoordinator>());
        services.AddSingleton<ISessionMonitor>(sp => sp.GetRequiredService<StreamSessionCoordinator>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<StreamSessionCoordinator>());

        // CQRS: commands, queries and their handlers, registered at compile time.
        services
            .AddBrilliantMediator()
            .AddGeneratedHandlers()
            .Build();

        return services;
    }
}
