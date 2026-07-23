using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nagare.ViewModels;
using Nagare.WinApp.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

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
///
/// <para>What little code-behind there is exists because the three things below need WinUI types the
/// ViewModel must never see: a dropped <c>StorageFile</c>, the clipboard, and the shell's
/// navigation.</para>
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

    // ------------------------------------------------------------------ drag & drop

    /// <summary>
    /// Postel's law, input side: anything that looks like a file is accepted here. Whether it can
    /// actually be broadcast is ffprobe's answer and the preflight's verdict — not a guess made from
    /// an extension.
    /// </summary>
    private void OnFileDragOver(object sender, DragEventArgs e)
    {
        if (ViewModel.IsSessionActive || !e.DataView.Contains(StandardDataFormats.StorageItems))
            return;   // AcceptedOperation stays None: the drop is refused, silently and visibly

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Diffuser cette vidéo";
    }

    private async void OnFileDrop(object sender, DragEventArgs e)
    {
        if (ViewModel.IsSessionActive || !e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var items = await e.DataView.GetStorageItemsAsync();

        // A multi-selection drop takes the first file: one broadcast, one source (SPEC §5).
        if (items.FirstOrDefault() is StorageFile file)
            await ViewModel.UseFileCommand.ExecuteAsync(file.Path);
    }

    // -------------------------------------------------------------------- clipboard

    /// <summary>
    /// Copies the command line — the MASKED one, and no other exists here: the ViewModel receives
    /// <c>FfmpegCommand.MaskedCommandLine</c> from the query and could not unmask it (ADR-0005).
    /// </summary>
    private void OnCopyCommand(object sender, RoutedEventArgs e) => CopyToClipboard(ViewModel.CommandPreview);

    /// <summary>
    /// Copies the log. Safe for the same reason: the coordinator scrubs the key out of every line
    /// before it is ever buffered.
    /// </summary>
    private void OnCopyLogs(object sender, RoutedEventArgs e)
        => CopyToClipboard(string.Join(Environment.NewLine, ViewModel.Logs));

    private static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetText(text);

        Clipboard.SetContent(package);
    }

    // ------------------------------------------------------------------- navigation

    /// <summary>
    /// The empty states name what is missing AND lead to it: told "no channel yet", the user reaches
    /// the page that fixes it in one click (Paradox of the Active User).
    /// </summary>
    private void OnGoToProfiles(object sender, RoutedEventArgs e) => NavigateTo("Profiles");

    private void OnGoToChannels(object sender, RoutedEventArgs e) => NavigateTo("Channels");

    private static void NavigateTo(string tag)
    {
        if (App.Current.Services.GetRequiredService<MainWindowContext>().Window is MainWindow shell)
            shell.NavigateTo(tag);
    }
}
