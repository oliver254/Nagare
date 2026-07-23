using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nagare.ViewModels;

namespace Nagare.WinApp.Views;

/// <summary>
/// Channels CRUD (plan §7, phase 4.1). Binding only — the logic lives in the ViewModel. The one
/// exception is the deletion, which asks for confirmation: a <c>ContentDialog</c> needs a
/// <c>XamlRoot</c>, and no WinUI type may enter Nagare.ViewModels.
/// </summary>
public sealed partial class ChannelsPage : Page
{
    public ChannelsPage()
    {
        InitializeComponent();

        ViewModel = App.Current.Services.GetRequiredService<ChannelsViewModel>();

        Loaded += OnLoaded;
    }

    public ChannelsViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Deleting a channel throws its stream key away with it, and the key cannot be read back from
    /// anywhere (ADR-0005) — it would have to be fetched from the platform again. The dialog names
    /// the channel and says so.
    /// </summary>
    private async void OnDeleteRequested(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedChannel is not { } channel)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Supprimer « {channel.Name} » ?",
            Content = "Ce channel et sa clé de stream seront définitivement supprimés.",
            PrimaryButtonText = "Supprimer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close   // the safe answer is the one under Enter
        };

        if (await dialog.ShowAsync() is ContentDialogResult.Primary)
            await ViewModel.DeleteCommand.ExecuteAsync(null);
    }
}
