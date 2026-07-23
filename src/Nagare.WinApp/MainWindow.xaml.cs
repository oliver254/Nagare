using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Nagare.WinApp.Views;
using Windows.Graphics;
using WinRT.Interop;

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

        SizeToContent();

        // Selected here rather than in XAML: the selection triggers the navigation, and the Frame
        // must already exist when it fires.
        Navigation.SelectedItem = Navigation.MenuItems[0];
    }

    /// <summary>Where the window starts. The user is free to resize afterwards.</summary>
    private const int PreferredWidthDips = 1280;

    private const int PreferredHeightDips = 900;

    /// <summary>
    /// Opens big enough for the dashboard: it is five regions tall and its log console needs room,
    /// and a first launch showing a squeezed journal is a first launch that looks broken.
    ///
    /// <para>Two corrections the plain <c>Resize(1280, 900)</c> was missing.
    /// <b>Scale:</b> <see cref="AppWindow.Resize"/> takes RAW PIXELS, not DIPs — on a 200% display
    /// that call produced 640×450 effective pixels, far smaller than the default it meant to replace.
    /// <b>Clamp:</b> the work area is not always bigger than the wish. On a 1366×768 laptop a 900 px
    /// request pushes the bottom of the page off-screen, and since the dashboard is deliberately not
    /// inside a ScrollViewer (its log list needs a bounded height) the Journal card and the Démarrer
    /// button simply become unreachable.</para>
    /// </summary>
    private void SizeToContent()
    {
        // Read from the HWND rather than from Content.XamlRoot: the XamlRoot is not guaranteed to be
        // attached yet this early, and a silent fallback to 1.0 would be the bug all over again.
        var dpi = GetDpiForWindow(WindowNative.GetWindowHandle(this));
        var scale = dpi is 0 ? 1.0 : dpi / 96.0;

        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;

        AppWindow.Resize(new SizeInt32(
            Math.Min((int)(PreferredWidthDips * scale), area.Width),
            Math.Min((int)(PreferredHeightDips * scale), area.Height)));
    }

    // DllImport et non LibraryImport : ce dernier exige AllowUnsafeBlocks sur tout le projet, un prix
    // hors de proportion avec un appel qui rend un entier.
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

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
