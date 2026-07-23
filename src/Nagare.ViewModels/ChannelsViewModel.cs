using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Monbsoft.BrilliantMediator.Abstractions;
using Nagare.Application.Channels;
using Nagare.Domain.Channels;
using Nagare.Domain.Common;

namespace Nagare.ViewModels;

/// <summary>
/// CRUD of the broadcast channels (plan §7, phase 4.1).
///
/// THE STREAM KEY IS NEVER READ BACK. <see cref="ChannelDto"/> does not carry it — only
/// <c>KeyConfigured</c> — and there is no query that could return it: the plaintext exists nowhere
/// but in Infrastructure, behind Data Protection (ADR-0005). So editing an existing channel always
/// starts with an EMPTY key field, and leaving it empty means "keep the current key" — exactly the
/// contract of <see cref="SaveChannelCommand.PlaintextKey"/> being null.
/// </summary>
public sealed partial class ChannelsViewModel : ViewModelBase
{
    private readonly IMediator _mediator;

    public ChannelsViewModel(IMediator mediator) => _mediator = mediator;

    public ObservableCollection<ChannelDto> Channels { get; } = [];

    public IReadOnlyList<Platform> Platforms { get; } = Enum.GetValues<Platform>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DeleteCommand))]
    private ChannelDto? _selectedChannel;

    /// <summary>
    /// Nothing to list. Drives the empty state — the first launch has no channel, and a blank list
    /// that says nothing is where a new user stops.
    /// </summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isEditing;

    /// <summary>True while creating: the key is then REQUIRED (the domain refuses a channel without one).</summary>
    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private Platform _editPlatform;

    [ObservableProperty]
    private string _editBaseUrl = string.Empty;

    /// <summary>
    /// Plaintext typed in the PasswordBox, held only until <see cref="SaveAsync"/> hands it to the
    /// command — which protects it and forgets it. Cleared right after. Empty = key unchanged.
    /// </summary>
    [ObservableProperty]
    private string _editStreamKey = string.Empty;

    private ChannelId? _editingId;

    [RelayCommand]
    private Task LoadAsync() => RunGuardedAsync(async () =>
    {
        var channels = await _mediator.SendAsync<GetChannelsQuery, IReadOnlyList<ChannelDto>>(new GetChannelsQuery());

        Channels.Clear();
        foreach (var channel in channels)
            Channels.Add(channel);

        IsEmpty = Channels.Count == 0;
    });

    [RelayCommand]
    private void New()
    {
        ErrorMessage = null;
        _editingId = null;
        IsCreating = true;
        IsEditing = true;

        EditName = string.Empty;
        EditPlatform = Platform.Twitch;
        EditBaseUrl = PlatformDefaults.DefaultBaseUrl(Platform.Twitch) ?? string.Empty;
        EditStreamKey = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Edit()
    {
        var channel = SelectedChannel!;

        ErrorMessage = null;
        _editingId = channel.Id;
        IsCreating = false;
        IsEditing = true;

        EditName = channel.Name;
        EditPlatform = channel.Platform;   // may prefill the base url...
        EditBaseUrl = channel.BaseUrl;     // ...so the stored one is restored right after
        EditStreamKey = string.Empty;      // NEVER pre-filled: the key cannot be read back
    }

    [RelayCommand(CanExecute = nameof(IsEditing))]
    private Task SaveAsync() => RunGuardedAsync(async () =>
    {
        // Empty field = key unchanged. On creation the command itself refuses a missing key
        // (the domain owns that rule; the UI does not restate it).
        var plaintextKey = string.IsNullOrEmpty(EditStreamKey) ? null : EditStreamKey;

        await _mediator.DispatchAsync<SaveChannelCommand, ChannelId>(
            new SaveChannelCommand(_editingId, EditName, EditPlatform, EditBaseUrl, plaintextKey));

        EditStreamKey = string.Empty;   // the plaintext lives no longer than the call
        IsEditing = false;
        IsCreating = false;

        await LoadAsync();
    });

    [RelayCommand]
    private void Cancel()
    {
        IsEditing = false;
        IsCreating = false;
        EditStreamKey = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task DeleteAsync() => RunGuardedAsync(async () =>
    {
        await _mediator.DispatchAsync(new DeleteChannelCommand(SelectedChannel!.Id));

        IsEditing = false;
        await LoadAsync();
    });

    private bool HasSelection() => SelectedChannel is not null;

    /// <summary>
    /// Platform picked in the editor -> suggest its base URL (ARCHITECTURE §2.3: a suggestion, not
    /// an invariant — the user stays free to edit it). Custom RTMP has no default: leave the field.
    /// </summary>
    partial void OnEditPlatformChanged(Platform value)
    {
        if (PlatformDefaults.DefaultBaseUrl(value) is { } defaultUrl)
            EditBaseUrl = defaultUrl;
    }
}
