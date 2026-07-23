using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nagare.ViewModels;

namespace Nagare.WinApp.Views;

/// <summary>
/// Encoding profiles CRUD (plan §7, phase 4.2). Binding only — except the deletion, which asks for
/// confirmation first: a <c>ContentDialog</c> needs a <c>XamlRoot</c>, a WinUI type the ViewModel
/// must never see.
/// </summary>
public sealed partial class ProfilesPage : Page
{
    public ProfilesPage()
    {
        InitializeComponent();

        ViewModel = App.Current.Services.GetRequiredService<ProfilesViewModel>();

        Loaded += OnLoaded;
    }

    public ProfilesViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    /// <summary>One dialog at a time — see <see cref="OnDeleteRequested"/>.</summary>
    private bool _confirming;

    /// <summary>
    /// A deletion is irreversible and used to happen on the click itself. The dialog NAMES what is
    /// about to disappear — "are you sure?" alone tells the user nothing they can check.
    ///
    /// <para>The guard is what keeps this <c>async void</c> handler from killing the application:
    /// WinUI allows a single <c>ContentDialog</c> at a time and throws otherwise, and a double-click
    /// — or the Suppr accelerator repeating under a held key — reaches here twice. An exception out
    /// of an <c>async void</c> has nowhere to go but the process.</para>
    /// </summary>
    private async void OnDeleteRequested(object sender, RoutedEventArgs e)
    {
        if (_confirming || ViewModel.SelectedProfile is not { } profile)
            return;

        _confirming = true;

        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = $"Supprimer « {profile.Name} » ?",
                Content = "Ce profil d'encodage sera définitivement supprimé.",
                PrimaryButtonText = "Supprimer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Close   // the safe answer is the one under Enter
            };

            if (await dialog.ShowAsync() is ContentDialogResult.Primary)
                await ViewModel.DeleteCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = $"Suppression impossible : {ex.Message}";
        }
        finally
        {
            _confirming = false;
        }
    }
}
