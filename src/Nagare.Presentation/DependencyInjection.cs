using Microsoft.Extensions.DependencyInjection;
using Nagare.Presentation.ViewModels;

namespace Nagare.Presentation;

/// <summary>
/// DI registration of the presentation layer (plan §7, phase 4).
///
/// The ViewModels are TRANSIENT: one per page instance, created when the page loads and disposed
/// when it unloads. <see cref="DashboardViewModel"/> subscribes to <c>ISessionMonitor</c>, and a
/// dead ViewModel still hanging off the coordinator's events would leak for the lifetime of the
/// application. Nothing is lost by dropping it: the coordinator keeps the truth, and the page
/// rehydrates from <c>Current</c> + <c>RecentLogs</c> when it comes back.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNagarePresentation(this IServiceCollection services)
    {
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProfilesViewModel>();
        services.AddTransient<ChannelsViewModel>();

        return services;
    }
}
