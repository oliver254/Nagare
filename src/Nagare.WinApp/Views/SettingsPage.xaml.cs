using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nagare.ViewModels;

namespace Nagare.WinApp.Views;

/// <summary>
/// ffmpeg configuration screen (ADR-0010). Binding only: the detection, the test and the save are
/// three commands of <see cref="SettingsViewModel"/>, and the file dialog reaches WinUI through
/// <c>IExecutableFilePicker</c> — the ViewModel never sees a picker.
/// </summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();

        ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();

        Loaded += OnLoaded;
    }

    public SettingsViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }
}
