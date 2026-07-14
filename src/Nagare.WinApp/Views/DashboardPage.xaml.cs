using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nagare.Presentation;
using Nagare.Presentation.ViewModels;

namespace Nagare.WinApp.Views;

/// <summary>
/// Broadcast page (plan §7, phases 4.3 and 5). The page owns NO logic: it binds, loads, and — this
/// one matters — DISPOSES its ViewModel on unload, which unsubscribes it from
/// <c>ISessionMonitor</c>. A page navigated away from that stays subscribed leaks for the lifetime
/// of the application and keeps pushing updates into a dead visual tree.
///
/// <para>The ViewModel is built through <see cref="DependencyInjection.CreateDashboard"/> rather
/// than resolved from the container: a transient IDisposable resolved from the ROOT provider is kept
/// in that provider's disposables list for the life of the application. Disposing it here would not
/// be enough — the container would still hold the reference, and every visit to this page would pin
/// another dead ViewModel with its 500 buffered log lines. Here the page owns it outright.</para>
/// </summary>
public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();

        ViewModel = DependencyInjection.CreateDashboard(App.Current.Services);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public DashboardViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
        => await ViewModel.LoadCommand.ExecuteAsync(null);

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

        ViewModel.Dispose();
    }
}
