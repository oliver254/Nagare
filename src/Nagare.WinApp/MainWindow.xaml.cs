using Microsoft.UI.Xaml.Controls;
using Nagare.WinApp.Views;
using Windows.Graphics;

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

        // The dashboard is five regions tall and its log console needs room; the default WinUI window
        // opens far too small for it, and a first launch that shows a squeezed log is a first launch
        // that looks broken. The user is free to resize — this only sets where it starts.
        AppWindow.Resize(new SizeInt32(1280, 900));

        // Selected here rather than in XAML: the selection triggers the navigation, and the Frame
        // must already exist when it fires.
        Navigation.SelectedItem = Navigation.MenuItems[0];
    }

    /// <summary>
    /// Sends the shell to a page by its tag. It exists for the dashboard's empty states: told that no
    /// channel exists, the user must be able to go and create one from where the gap was named
    /// (Paradox of the Active User) — not be left to find the rail on their own.
    /// </summary>
    public void NavigateTo(string tag)
    {
        foreach (var item in Navigation.MenuItems)
        {
            if (item is NavigationViewItem { Tag: string itemTag } menuItem && itemTag == tag)
            {
                Navigation.SelectedItem = menuItem;   // the selection is what triggers the navigation
                return;
            }
        }
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
