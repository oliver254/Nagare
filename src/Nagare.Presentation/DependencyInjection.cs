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
///
/// <para><b><see cref="DashboardViewModel"/> is deliberately NOT registered.</b> It is the only
/// ViewModel that is <see cref="IDisposable"/>, and Microsoft.Extensions.DependencyInjection tracks
/// every transient disposable in the disposables list of the scope that RESOLVED it. The pages
/// resolve from the ROOT provider, so each visit to the dashboard would leave a dead ViewModel —
/// and its 500 buffered log lines — pinned for the life of the application, even though the page
/// disposes it correctly. It is built with <see cref="CreateDashboard"/> instead, which injects the
/// dependencies without handing ownership to the container: the page owns it, the page disposes
/// it.</para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNagarePresentation(this IServiceCollection services)
    {
        services.AddTransient<ProfilesViewModel>();
        services.AddTransient<ChannelsViewModel>();

        return services;
    }

    /// <summary>
    /// Builds the dashboard ViewModel WITHOUT the container taking ownership of its disposal.
    /// The caller (the page) owns it and must dispose it — see the class remarks above.
    /// </summary>
    public static DashboardViewModel CreateDashboard(IServiceProvider services)
        => ActivatorUtilities.CreateInstance<DashboardViewModel>(services);
}
