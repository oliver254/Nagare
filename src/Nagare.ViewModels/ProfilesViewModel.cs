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
    // Shown in the width/height/fps boxes while "Forcer…" is off, so the field is never blank and
    // turning the switch on starts from something sane. They are UI defaults, not domain rules.
    private const double DefaultWidth = 1920;
    private const double DefaultHeight = 1080;
    private const double DefaultFps = 30;

    private readonly IMediator _mediator;

    public ProfilesViewModel(IMediator mediator) => _mediator = mediator;

    public ObservableCollection<StreamProfileDto> Profiles { get; } = [];

    public IReadOnlyList<VideoCodec> VideoCodecs { get; } = Enum.GetValues<VideoCodec>();
    public IReadOnlyList<RateControl> RateControls { get; } = Enum.GetValues<RateControl>();
    public IReadOnlyList<AudioCodec> AudioCodecs { get; } = Enum.GetValues<AudioCodec>();
    public IReadOnlyList<int> SampleRates { get; } = AudioSettings.AllowedSampleRates;

    /// <summary>Presets valid for the selected codec — the very list invariant E6 checks.</summary>
    public ObservableCollection<string> AvailablePresets { get; } = [];

    /// <summary>Ready-made starting points for the editor — see <see cref="ProfileTemplate"/>.</summary>
    public IReadOnlyList<ProfileTemplate> Templates { get; } = ProfileTemplate.All;

    /// <summary>
    /// Picking one fills the editor and nothing else: no profile is created, nothing is saved, every
    /// field stays editable. Reset to null whenever the editor opens, so it reads as "start from…"
    /// rather than as a claim about what is currently in the form.
    /// </summary>
    [ObservableProperty]
    private ProfileTemplate? _selectedTemplate;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DeleteCommand))]
    private StreamProfileDto? _selectedProfile;

    /// <summary>
    /// Nothing to list. Drives the empty state, which is the only documentation this application has:
    /// nobody reads a manual, so the blank list must name the next action itself.
    /// </summary>
    [ObservableProperty]
    private bool _isEmpty = true;

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

        IsEmpty = Profiles.Count == 0;
    });

    [RelayCommand]
    private void New()
    {
        ErrorMessage = null;
        _editingId = null;
        IsEditing = true;
        SelectedTemplate = null;

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
        EditWidth = DefaultWidth;
        EditHeight = DefaultHeight;
        EditHasFps = false;
        EditFps = DefaultFps;
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
        SelectedTemplate = null;

        EditName = profile.Name;

        Populate(profile.Video, profile.Audio, profile.Input);
    }

    /// <summary>
    /// Pours a set of value objects into the editor fields. The ONE place that knows how a profile
    /// becomes a form — an existing profile and a template are the same operation, and they were two
    /// copies of eighteen assignments that had already drifted apart: the copy used by the templates
    /// fell back to whatever the fields happened to hold, so picking a template without a resolution
    /// after one that had it left the previous template's dimensions behind.
    ///
    /// <para>Order matters: the codec first, then the preset list it commands, then the preset — set
    /// the other way round and the value is dropped for not being in the list yet.</para>
    /// </summary>
    private void Populate(EncodingSettings video, AudioSettings audio, InputOptions input)
    {
        EditCodec = video.Codec;
        RefreshPresets();                    // no-op when the codec hook above already did it
        EditPreset = video.Preset;           // after RefreshPresets: the list must hold the value
        EditRateControl = video.RateControl;
        EditBitrateKbps = video.BitrateKbps;
        EditMaxrateKbps = video.MaxrateKbps;
        EditBufsizeKbps = video.BufsizeKbps;
        EditGopSize = video.GopSize;
        EditKeyintMin = video.KeyintMin;

        EditHasResolution = video.Resolution is not null;
        EditWidth = video.Resolution?.Width ?? DefaultWidth;
        EditHeight = video.Resolution?.Height ?? DefaultHeight;

        EditHasFps = video.Fps is not null;
        EditFps = video.Fps ?? DefaultFps;

        EditAudioCodec = audio.Codec;
        EditAudioBitrateKbps = audio.BitrateKbps;
        EditSampleRateHz = audio.SampleRateHz;

        EditReadAtNativeRate = input.ReadAtNativeRate;
        EditLoopInfinitely = input.LoopInfinitely;
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
    /// Pours a template into the editor — the same fill as <see cref="Edit"/>, see
    /// <see cref="Populate"/>.
    ///
    /// <para>The name is filled only while it is still blank: a template applied to a profile the
    /// user has already named must not rename it behind their back.</para>
    /// </summary>
    partial void OnSelectedTemplateChanged(ProfileTemplate? value)
    {
        if (value is null)
            return;

        if (string.IsNullOrWhiteSpace(EditName))
            EditName = value.Name;

        Populate(value.Video, value.Audio, value.Input);
    }

    /// <summary>
    /// nvenc and libx264 do not share a single preset name. Switching codec therefore reloads the
    /// list from the domain and drops a preset that no longer exists.
    ///
    /// <para>It is called BOTH from the <see cref="EditCodec"/> hook and explicitly by
    /// <see cref="Populate"/> and <see cref="New"/>, and that redundancy is deliberate: the hook does
    /// not fire when the codec is assigned its current value, which is exactly the case on the first
    /// <c>Modifier</c> of an <c>H264Nvenc</c> profile — <c>AvailablePresets</c> would still be empty
    /// and the preset would be dropped for not being in the list. So the guard is here instead: a
    /// list already holding the right values is left ALONE, which spares the bound ComboBox a
    /// pointless teardown and the null selection it pushes back on Clear().</para>
    /// </summary>
    private void RefreshPresets()
    {
        var presets = EncodingSettings.PresetsFor(EditCodec);

        if (!AvailablePresets.SequenceEqual(presets))
        {
            AvailablePresets.Clear();
            foreach (var preset in presets)
                AvailablePresets.Add(preset);
        }

        if (string.IsNullOrEmpty(EditPreset) || !presets.Contains(EditPreset))
            EditPreset = presets[0];
    }

    /// <summary>
    /// An empty NumberBox yields NaN. Mapping it to 0 keeps the arbitration where it belongs: the
    /// domain answers "E1: bitrate must be strictly positive" instead of the UI inventing a message.
    /// </summary>
    private static int ToInt(double value) => double.IsNaN(value) ? 0 : (int)Math.Round(value);
}
