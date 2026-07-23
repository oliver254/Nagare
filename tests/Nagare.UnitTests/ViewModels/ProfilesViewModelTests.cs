using Nagare.Application.Profiles;
using Nagare.Domain.Common;
using Nagare.Domain.Profiles;
using Nagare.ViewModels;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.ViewModels;

/// <summary>
/// The profile editor must NOT restate the encoding rules: it builds the value objects and lets the
/// domain arbitrate (invariants E1-E8). These tests pin exactly that.
/// </summary>
public sealed class ProfilesViewModelTests
{
    private static readonly StreamProfileDto Existing = StreamProfileDto.From(
        StreamProfile.Create(
            "1080p NVENC",
            new EncodingSettings(VideoCodec.H264Nvenc, "p2", RateControl.Cbr, 3000, 3000, 3000, 60, 60, null, null),
            new AudioSettings(AudioCodec.Aac, 128, 48000),
            InputOptions.Default));

    [Fact]
    public async Task A_valid_profile_is_saved_with_every_field()
    {
        var (vm, mediator) = await CreateLoadedAsync();

        vm.NewCommand.Execute(null);
        vm.EditName = "720p x264";
        vm.EditCodec = VideoCodec.Libx264;
        vm.EditPreset = "veryfast";
        vm.EditRateControl = RateControl.Vbr;
        vm.EditBitrateKbps = 2500;
        vm.EditMaxrateKbps = 3000;
        vm.EditBufsizeKbps = 6000;
        vm.EditGopSize = 50;
        vm.EditKeyintMin = 25;
        vm.EditHasResolution = true;
        vm.EditWidth = 1280;
        vm.EditHeight = 720;
        vm.EditHasFps = true;
        vm.EditFps = 25;
        vm.EditAudioBitrateKbps = 160;
        vm.EditSampleRateHz = 44100;
        vm.EditReadAtNativeRate = true;
        vm.EditLoopInfinitely = false;

        await vm.SaveCommand.ExecuteAsync(null);

        var command = mediator.Single<SaveStreamProfileCommand>();

        Assert.Null(command.Id);   // creation
        Assert.Equal("720p x264", command.Name);
        Assert.Equal(VideoCodec.Libx264, command.Video.Codec);
        Assert.Equal("veryfast", command.Video.Preset);
        Assert.Equal(RateControl.Vbr, command.Video.RateControl);
        Assert.Equal(2500, command.Video.BitrateKbps);
        Assert.Equal(3000, command.Video.MaxrateKbps);
        Assert.Equal(6000, command.Video.BufsizeKbps);
        Assert.Equal(50, command.Video.GopSize);
        Assert.Equal(25, command.Video.KeyintMin);
        Assert.Equal(new Resolution(1280, 720), command.Video.Resolution);
        Assert.Equal(25, command.Video.Fps);
        Assert.Equal(160, command.Audio.BitrateKbps);
        Assert.Equal(44100, command.Audio.SampleRateHz);
        Assert.Equal(new InputOptions(ReadAtNativeRate: true, LoopInfinitely: false), command.Input);
        Assert.Null(vm.ErrorMessage);
    }

    /// <summary>
    /// E4 (bufsize >= bitrate) is rejected BY THE DOMAIN. The ViewModel shows its message and sends
    /// nothing — no rule is re-implemented in the UI.
    /// </summary>
    [Fact]
    public async Task An_invariant_violation_is_shown_and_nothing_is_saved()
    {
        var (vm, mediator) = await CreateLoadedAsync();

        vm.NewCommand.Execute(null);
        vm.EditName = "Bufsize trop petit";
        vm.EditBitrateKbps = 6000;
        vm.EditMaxrateKbps = 6000;
        vm.EditBufsizeKbps = 3000;   // < bitrate

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.StartsWith("E4:", vm.ErrorMessage);
        Assert.Empty(mediator.Sent.OfType<SaveStreamProfileCommand>());
        Assert.True(vm.IsEditing);   // the editor stays open so the value can be fixed
    }

    [Fact]
    public async Task An_unknown_sample_rate_is_refused_by_the_domain()
    {
        var (vm, mediator) = await CreateLoadedAsync();

        vm.NewCommand.Execute(null);
        vm.EditName = "Sample rate exotique";
        vm.EditSampleRateHz = 32000;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Sample rate must be 44100 or 48000 Hz.", vm.ErrorMessage);
        Assert.Empty(mediator.Sent.OfType<SaveStreamProfileCommand>());
    }

    /// <summary>The preset list comes from the domain — nvenc and libx264 do not share one.</summary>
    [Fact]
    public async Task Presets_follow_the_selected_codec()
    {
        var (vm, _) = await CreateLoadedAsync();

        vm.NewCommand.Execute(null);

        Assert.Equal(EncodingSettings.PresetsFor(VideoCodec.H264Nvenc), vm.AvailablePresets);
        Assert.Equal("p1", vm.EditPreset);

        vm.EditCodec = VideoCodec.Libx264;

        Assert.Equal(EncodingSettings.PresetsFor(VideoCodec.Libx264), vm.AvailablePresets);
        Assert.Equal("ultrafast", vm.EditPreset);   // an nvenc preset would no longer be valid (E6)
    }

    /// <summary>
    /// Edit as the FIRST action, on an H264Nvenc profile — which is also EditCodec's default, so
    /// assigning it raises no change and the codec hook does not fire. This is why Populate calls
    /// RefreshPresets explicitly even though the hook usually does: without it AvailablePresets would
    /// still be empty here and "p2" would be dropped for not being in the list.
    /// </summary>
    [Fact]
    public async Task Editing_loads_every_field_of_the_profile()
    {
        var (vm, mediator) = await CreateLoadedAsync();

        vm.SelectedProfile = vm.Profiles.Single();
        vm.EditCommand.Execute(null);

        Assert.Equal("1080p NVENC", vm.EditName);
        Assert.Equal(VideoCodec.H264Nvenc, vm.EditCodec);
        Assert.Equal("p2", vm.EditPreset);
        Assert.Equal(3000, vm.EditBitrateKbps);
        Assert.False(vm.EditHasResolution);
        Assert.False(vm.EditHasFps);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(Existing.Id, mediator.Single<SaveStreamProfileCommand>().Id);   // update, not creation
    }

    // ------------------------------------------------------------------------ templates

    /// <summary>
    /// The templates are built from the domain's own value objects, so this is not a formality: a
    /// template violating E1-E8 could not be constructed at all, and the list would fail to load.
    /// Both encoder families are represented — a machine without NVENC must still have an answer.
    /// </summary>
    [Fact]
    public void Every_template_is_a_profile_the_domain_accepts()
    {
        Assert.NotEmpty(ProfileTemplate.All);
        Assert.Contains(ProfileTemplate.All, t => !t.Video.Codec.RequiresNvenc());
        Assert.Contains(ProfileTemplate.All, t => t.Video.Codec.RequiresNvenc());

        // Reconstructing each one re-runs every invariant, on this thread, with the failure visible.
        foreach (var template in ProfileTemplate.All)
        {
            _ = new EncodingSettings(
                template.Video.Codec, template.Video.Preset, template.Video.RateControl,
                template.Video.BitrateKbps, template.Video.MaxrateKbps, template.Video.BufsizeKbps,
                template.Video.GopSize, template.Video.KeyintMin, template.Video.Resolution, template.Video.Fps);
        }
    }

    [Fact]
    public async Task A_template_fills_the_editor_and_saves_as_it_stands()
    {
        var (vm, mediator) = await CreateLoadedAsync();

        vm.NewCommand.Execute(null);
        vm.SelectedTemplate = vm.Templates.First(t => t.Video.Codec == VideoCodec.Libx264);

        Assert.Equal(vm.SelectedTemplate.Name, vm.EditName);   // an empty name takes the template's
        Assert.Equal("veryfast", vm.EditPreset);               // the preset list followed the codec
        Assert.True(vm.EditHasResolution);

        await vm.SaveCommand.ExecuteAsync(null);

        var command = mediator.Single<SaveStreamProfileCommand>();
        Assert.Equal(vm.SelectedTemplate.Video, command.Video);
        Assert.Equal(vm.SelectedTemplate.Audio, command.Audio);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task A_template_never_renames_a_profile_the_user_has_named()
    {
        var (vm, _) = await CreateLoadedAsync();

        vm.NewCommand.Execute(null);
        vm.EditName = "Mon réglage";
        vm.SelectedTemplate = vm.Templates[0];

        Assert.Equal("Mon réglage", vm.EditName);
    }

    /// <summary>Opening the editor clears the template: it offers a start, it does not report one.</summary>
    [Fact]
    public async Task Opening_the_editor_clears_the_template()
    {
        var (vm, _) = await CreateLoadedAsync();

        vm.NewCommand.Execute(null);
        vm.SelectedTemplate = vm.Templates[0];

        vm.SelectedProfile = vm.Profiles.Single();
        vm.EditCommand.Execute(null);

        Assert.Null(vm.SelectedTemplate);
        Assert.Equal("1080p NVENC", vm.EditName);   // the template did not leak into the loaded profile
    }

    // ---------------------------------------------------------------------- empty state

    [Fact]
    public async Task A_blank_install_reports_an_empty_list()
    {
        var (vm, _) = await CreateLoadedAsync(empty: true);

        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task A_populated_list_is_not_empty()
    {
        var (vm, _) = await CreateLoadedAsync();

        Assert.False(vm.IsEmpty);
    }

    private static async Task<(ProfilesViewModel Vm, FakeMediator Mediator)> CreateLoadedAsync(bool empty = false)
    {
        IReadOnlyList<StreamProfileDto> profiles = empty ? [] : [Existing];

        var mediator = new FakeMediator()
            .Answer<GetStreamProfilesQuery>(profiles)
            .Answer<SaveStreamProfileCommand>(ProfileId.New());

        var vm = new ProfilesViewModel(mediator);
        await vm.LoadCommand.ExecuteAsync(null);

        return (vm, mediator);
    }
}
