using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nagare.Presentation.ViewModels;

namespace Nagare.WinApp.Views;

/// <summary>Channels CRUD (plan §7, phase 4.1). Binding only — the logic lives in the ViewModel.</summary>
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
}
