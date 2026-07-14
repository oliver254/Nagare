using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nagare.Presentation.ViewModels;

namespace Nagare.WinApp.Views;

/// <summary>Encoding profiles CRUD (plan §7, phase 4.2). Binding only.</summary>
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
}
