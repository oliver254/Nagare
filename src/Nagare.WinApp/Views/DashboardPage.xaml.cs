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
        ActualThemeChanged += OnThemeChanged;
    }

    public DashboardViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
        => await ViewModel.LoadCommand.ExecuteAsync(null);

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        ActualThemeChanged -= OnThemeChanged;

        ViewModel.Dispose();
    }

    /// <summary>
    /// Re-runs the one-way bindings so the converter-produced colours follow the system theme.
    ///
    /// <para>A <c>{ThemeResource}</c> in markup is re-evaluated by the framework; a brush a converter
    /// looked up and returned is not (see <c>ThemeBrushes</c>). Without this, switching Windows to
    /// dark mode left the health badge and the speed/drops tiles painted for the light one — the very
    /// accessibility guarantee docs/design/ux-ui.md §7 claims.</para>
    /// </summary>
    private void OnThemeChanged(FrameworkElement sender, object args) => Bindings.Update();

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

    /// <summary>
    /// The deferral is not optional. Awaiting inside a Drop handler returns to the framework, which
    /// then considers the drop over and tears the operation down — <c>GetStorageItemsAsync</c> would
    /// be racing an invalidated <c>DataView</c> and would either come back empty (a dropped file
    /// silently ignored) or throw, and a throw out of an <c>async void</c> handler takes the window
    /// with it. Holding a deferral keeps the operation alive until the read is done.
    /// </summary>
    private async void OnFileDrop(object sender, DragEventArgs e)
    {
        if (ViewModel.IsSessionActive || !e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var deferral = e.GetDeferral();

        try
        {
            var items = await e.DataView.GetStorageItemsAsync();

            // A multi-selection drop takes the first file: one broadcast, one source (SPEC §5).
            if (items.FirstOrDefault() is StorageFile file)
                await ViewModel.UseFileCommand.ExecuteAsync(file.Path);
        }
        catch (Exception ex)
        {
            // A shell that hands over something unreadable is not a reason to lose the window.
            ViewModel.ErrorMessage = $"Fichier déposé illisible : {ex.Message}";
        }
        finally
        {
            deferral.Complete();
        }
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

    private void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetText(text);

        try
        {
            Clipboard.SetContent(package);

            // Without Flush the content belongs to THIS process and Windows discards it on exit —
            // which is precisely the "copy the log, close Nagare, paste it in a report" flow the
            // button exists for. Flush hands ownership to the system.
            Clipboard.Flush();
        }
        catch (Exception ex)
        {
            // The clipboard is a shared, lockable resource: another process holding it makes these
            // calls throw, and a copy button is not worth a crash.
            ViewModel.ErrorMessage = $"Copie impossible : {ex.Message}";
        }
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
