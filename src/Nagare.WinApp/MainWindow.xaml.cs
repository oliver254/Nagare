using Microsoft.UI.Xaml.Controls;
using Nagare.WinApp.Views;

namespace Nagare.WinApp;

/// <summary>
/// Shell of the application: a <see cref="NavigationView"/> driving a <see cref="Frame"/>.
/// The three pages carry the business views; a fourth entry (Planifications) is expected in
/// iteration 2 — see docs/product/stream-scheduling.md.
/// </summary>
public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "Nagare";

        // Selected here rather than in XAML: the selection triggers the navigation, and the Frame
        // must already exist when it fires.
        Navigation.SelectedItem = Navigation.MenuItems[0];
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag })
            return;

        var pageType = tag switch
        {
            "Dashboard" => typeof(DashboardPage),
            "Profiles" => typeof(ProfilesPage),
            "Channels" => typeof(ChannelsPage),
            _ => null
        };

        if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }
}
