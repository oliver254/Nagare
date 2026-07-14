using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nagare.Presentation.ViewModels;

namespace Nagare.WinApp.Views;

/// <summary>
/// Broadcast page (plan §7, phases 4.3 and 5). The page owns NO logic: it binds, loads, and — this
/// one matters — DISPOSES its ViewModel on unload, which unsubscribes it from
/// <c>ISessionMonitor</c>. A page navigated away from that stays subscribed leaks for the lifetime
/// of the application and keeps pushing updates into a dead visual tree.
/// </summary>
public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();

        ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();

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
