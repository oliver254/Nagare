using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Monbsoft.BrilliantMediator.Abstractions;
using Nagare.Application.Profiles;
using Nagare.Domain.Common;
using Nagare.Domain.Profiles;

namespace Nagare.ViewModels;

/// <summary>
/// CRUD of the encoding profiles (plan §7, phase 4.2).
///
/// NO VALIDATION HERE. The editor builds the value objects and lets them speak: invariants E1-E8
/// (<see cref="EncodingSettings"/>) and the audio rules throw <see cref="DomainException"/>, whose
/// message goes straight to the InfoBar. Re-implementing "bufsize >= bitrate" in the UI would
/// create a second source of truth, free to drift away from the domain.
///
/// The lists offered (presets per codec, sample rates) are READ FROM the domain for the same reason.
/// </summary>
public sealed partial class ProfilesViewModel : ViewModelBase
{
    private readonly IMediator _mediator;

    public ProfilesViewModel(IMediator mediator) => _mediator = mediator;

    public ObservableCollection<StreamProfileDto> Profiles { get; } = [];

    public IReadOnlyList<VideoCodec> VideoCodecs { get; } = Enum.GetValues<VideoCodec>();
    public IReadOnlyList<RateControl> RateControls { get; } = Enum.GetValues<RateControl>();
    public IReadOnlyList<AudioCodec> AudioCodecs { get; } = Enum.GetValues<AudioCodec>();
    public IReadOnlyList<int> SampleRates { get; } = AudioSettings.AllowedSampleRates;

    /// <summary>Presets valid for the selected codec — the very list invariant E6 checks.</summary>
    public ObservableCollection<string> AvailablePresets { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DeleteCommand))]
    private StreamProfileDto? _selectedProfile;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isEditing;

    [ObservableProperty]
    private string _editName = string.Empty;

    // --- video

    [ObservableProperty]
    private VideoCodec _editCodec;

    [ObservableProperty]
    private string _editPreset = string.Empty;

    [ObservableProperty]
    private RateControl _editRateControl;

    // NumberBox binds to a double; the cast back to int happens at save time, and the domain
    // rejects whatever makes no sense (E1, E5, E8).
    [ObservableProperty]
    private double _editBitrateKbps = 3000;

    [ObservableProperty]
    private double _editMaxrateKbps = 3000;

    [ObservableProperty]
    private double _editBufsizeKbps = 3000;

    [ObservableProperty]
    private double _editGopSize = 60;

    [ObservableProperty]
    private double _editKeyintMin = 60;

    [ObservableProperty]
    private bool _editHasResolution;

    [ObservableProperty]
    private double _editWidth = 1920;

    [ObservableProperty]
    private double _editHeight = 1080;

    [ObservableProperty]
    private bool _editHasFps;

    [ObservableProperty]
    private double _editFps = 30;

    // --- audio

    [ObservableProperty]
    private AudioCodec _editAudioCodec;

    [ObservableProperty]
    private double _editAudioBitrateKbps = 128;

    [ObservableProperty]
    private int _editSampleRateHz = 48000;

    // --- input

    [ObservableProperty]
    private bool _editReadAtNativeRate = true;

    [ObservableProperty]
    private bool _editLoopInfinitely = true;

    private ProfileId? _editingId;

    [RelayCommand]
    private Task LoadAsync() => RunGuardedAsync(async () =>
    {
        var profiles = await _mediator.SendAsync<GetStreamProfilesQuery, IReadOnlyList<StreamProfileDto>>(
            new GetStreamProfilesQuery());

        Profiles.Clear();
        foreach (var profile in profiles)
            Profiles.Add(profile);
    });

    [RelayCommand]
    private void New()
    {
        ErrorMessage = null;
        _editingId = null;
        IsEditing = true;

        EditName = string.Empty;
        EditCodec = VideoCodec.H264Nvenc;
        RefreshPresets();
        EditRateControl = RateControl.Cbr;
        EditBitrateKbps = 3000;
        EditMaxrateKbps = 3000;
        EditBufsizeKbps = 3000;
        EditGopSize = 60;
        EditKeyintMin = 60;
        EditHasResolution = false;
        EditWidth = 1920;
        EditHeight = 1080;
        EditHasFps = false;
        EditFps = 30;
        EditAudioCodec = AudioCodec.Aac;
        EditAudioBitrateKbps = 128;
        EditSampleRateHz = 48000;

        var input = InputOptions.Default;
        EditReadAtNativeRate = input.ReadAtNativeRate;
        EditLoopInfinitely = input.LoopInfinitely;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Edit()
    {
        var profile = SelectedProfile!;

        ErrorMessage = null;
        _editingId = profile.Id;
        IsEditing = true;

        EditName = profile.Name;

        EditCodec = profile.Video.Codec;
        RefreshPresets();
        EditPreset = profile.Video.Preset;   // after RefreshPresets: the list must hold the value
        EditRateControl = profile.Video.RateControl;
        EditBitrateKbps = profile.Video.BitrateKbps;
        EditMaxrateKbps = profile.Video.MaxrateKbps;
        EditBufsizeKbps = profile.Video.BufsizeKbps;
        EditGopSize = profile.Video.GopSize;
        EditKeyintMin = profile.Video.KeyintMin;

        EditHasResolution = profile.Video.Resolution is not null;
        EditWidth = profile.Video.Resolution?.Width ?? 1920;
        EditHeight = profile.Video.Resolution?.Height ?? 1080;

        EditHasFps = profile.Video.Fps is not null;
        EditFps = profile.Video.Fps ?? 30;

        EditAudioCodec = profile.Audio.Codec;
        EditAudioBitrateKbps = profile.Audio.BitrateKbps;
        EditSampleRateHz = profile.Audio.SampleRateHz;

        EditReadAtNativeRate = profile.Input.ReadAtNativeRate;
        EditLoopInfinitely = profile.Input.LoopInfinitely;
    }

    /// <summary>
    /// Builds the value objects — which validate themselves — then saves. A rejected invariant
    /// leaves the editor open with the domain's own message.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsEditing))]
    private Task SaveAsync() => RunGuardedAsync(async () =>
    {
        var video = new EncodingSettings(
            EditCodec,
            EditPreset,
            EditRateControl,
            ToInt(EditBitrateKbps),
            ToInt(EditMaxrateKbps),
            ToInt(EditBufsizeKbps),
            ToInt(EditGopSize),
            ToInt(EditKeyintMin),
            EditHasResolution ? new Resolution(ToInt(EditWidth), ToInt(EditHeight)) : null,
            EditHasFps ? ToInt(EditFps) : null);

        var audio = new AudioSettings(EditAudioCodec, ToInt(EditAudioBitrateKbps), EditSampleRateHz);
        var input = new InputOptions(EditReadAtNativeRate, EditLoopInfinitely);

        await _mediator.DispatchAsync<SaveStreamProfileCommand, ProfileId>(
            new SaveStreamProfileCommand(_editingId, EditName, video, audio, input));

        IsEditing = false;
        await LoadAsync();
    });

    [RelayCommand]
    private void Cancel()
    {
        IsEditing = false;
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task DeleteAsync() => RunGuardedAsync(async () =>
    {
        await _mediator.DispatchAsync(new DeleteStreamProfileCommand(SelectedProfile!.Id));

        IsEditing = false;
        await LoadAsync();
    });

    private bool HasSelection() => SelectedProfile is not null;

    partial void OnEditCodecChanged(VideoCodec value) => RefreshPresets();

    /// <summary>
    /// nvenc and libx264 do not share a single preset name. Switching codec therefore reloads the
    /// list from the domain and drops a preset that no longer exists.
    ///
    /// Clearing the collection makes the bound ComboBox push a null selection back into
    /// <see cref="EditPreset"/> — hence the emptiness check before picking a fallback.
    /// </summary>
    private void RefreshPresets()
    {
        var presets = EncodingSettings.PresetsFor(EditCodec);

        AvailablePresets.Clear();
        foreach (var preset in presets)
            AvailablePresets.Add(preset);

        if (string.IsNullOrEmpty(EditPreset) || !presets.Contains(EditPreset))
            EditPreset = presets[0];
    }

    /// <summary>
    /// An empty NumberBox yields NaN. Mapping it to 0 keeps the arbitration where it belongs: the
    /// domain answers "E1: bitrate must be strictly positive" instead of the UI inventing a message.
    /// </summary>
    private static int ToInt(double value) => double.IsNaN(value) ? 0 : (int)Math.Round(value);
}
