using System.Collections;
using System.Reflection;
using Nagare.Application.Channels;
using Nagare.Domain.Channels;
using Nagare.Domain.Common;
using Nagare.ViewModels;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.ViewModels;

/// <summary>
/// The stream key contract (ADR-0005, plan §6): a saved key is NEVER read back, and an empty field
/// means "unchanged" — never "erase it".
/// </summary>
public sealed class ChannelsViewModelTests
{
    /// <summary>A realistic key: a placeholder like "key" could pass a test for the wrong reason.</summary>
    private const string PlaintextKey = "live_2468_KpH2sAbCdEf";

    private static readonly ChannelDto Existing = new(
        ChannelId.New(), "Twitch principal", Platform.Twitch, "rtmp://live.twitch.tv/app", KeyConfigured: true);

    [Fact]
    public async Task Editing_a_channel_never_prefills_its_key()
    {
        var (vm, _) = await CreateLoadedAsync();

        vm.SelectedChannel = vm.Channels.Single();
        vm.EditCommand.Execute(null);

        Assert.Equal(Existing.Name, vm.EditName);
        Assert.Equal(Existing.BaseUrl, vm.EditBaseUrl);
        Assert.Equal(string.Empty, vm.EditStreamKey);   // the key cannot be read back — ever
    }

    [Fact]
    public async Task An_empty_key_field_means_unchanged()
    {
        var (vm, mediator) = await CreateLoadedAsync();

        vm.SelectedChannel = vm.Channels.Single();
        vm.EditCommand.Execute(null);
        vm.EditName = "Twitch renommé";

        await vm.SaveCommand.ExecuteAsync(null);

        var command = mediator.Single<SaveChannelCommand>();
        Assert.Equal(Existing.Id, command.Id);
        Assert.Equal("Twitch renommé", command.Name);
        Assert.Null(command.PlaintextKey);   // null = keep the current key (SaveChannelCommand's contract)
    }

    [Fact]
    public async Task A_typed_key_is_passed_once_then_forgotten()
    {
        var (vm, mediator) = await CreateLoadedAsync();

        vm.SelectedChannel = vm.Channels.Single();
        vm.EditCommand.Execute(null);
        vm.EditStreamKey = PlaintextKey;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(PlaintextKey, mediator.Single<SaveChannelCommand>().PlaintextKey);

        // The plaintext lives no longer than the call: nothing keeps it around afterwards.
        Assert.Equal(string.Empty, vm.EditStreamKey);
    }

    [Fact]
    public async Task Cancelling_wipes_the_typed_key()
    {
        var (vm, _) = await CreateLoadedAsync();

        vm.NewCommand.Execute(null);
        vm.EditStreamKey = PlaintextKey;

        vm.CancelCommand.Execute(null);

        Assert.Equal(string.Empty, vm.EditStreamKey);
    }

    /// <summary>
    /// The guard rail: NO readable member of the ViewModel may hand a key back. Written by
    /// reflection on purpose — a future property that would innocently expose one fails here.
    /// </summary>
    [Fact]
    public async Task No_property_of_the_view_model_ever_exposes_a_key()
    {
        var (vm, _) = await CreateLoadedAsync();

        vm.SelectedChannel = vm.Channels.Single();
        vm.EditCommand.Execute(null);
        vm.EditStreamKey = PlaintextKey;

        await vm.SaveCommand.ExecuteAsync(null);
        await vm.LoadCommand.ExecuteAsync(null);

        foreach (var property in typeof(ChannelsViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
                continue;

            var value = property.GetValue(vm);

            foreach (var text in Texts(value))
                Assert.DoesNotContain(PlaintextKey, text);
        }
    }

    [Fact]
    public async Task Selecting_a_platform_prefills_its_base_url()
    {
        var (vm, _) = await CreateLoadedAsync();

        vm.NewCommand.Execute(null);
        Assert.Equal(PlatformDefaults.TwitchBaseUrl, vm.EditBaseUrl);

        vm.EditPlatform = Platform.YouTube;
        Assert.Equal(PlatformDefaults.YouTubeBaseUrl, vm.EditBaseUrl);

        // Custom RTMP has no default: the user's URL is left alone.
        vm.EditBaseUrl = "rtmp://my.server/live";
        vm.EditPlatform = Platform.CustomRtmp;
        Assert.Equal("rtmp://my.server/live", vm.EditBaseUrl);
    }

    /// <summary>A refusal by the domain becomes a message, not a crash.</summary>
    [Fact]
    public async Task A_domain_refusal_is_shown_in_the_info_bar()
    {
        var (vm, mediator) = await CreateLoadedAsync();

        mediator.Answer<SaveChannelCommand>(_ => throw new DomainException("Base URL must use the rtmp:// or rtmps:// scheme."));

        vm.NewCommand.Execute(null);
        vm.EditName = "Bancal";
        vm.EditBaseUrl = "http://nope";
        vm.EditStreamKey = PlaintextKey;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Base URL must use the rtmp:// or rtmps:// scheme.", vm.ErrorMessage);
        Assert.True(vm.IsEditing);   // the editor stays open on the faulty value
    }

    private static IEnumerable<string> Texts(object? value)
    {
        switch (value)
        {
            case string text:
                yield return text;
                break;

            case IEnumerable items:
                foreach (var item in items)
                {
                    if (item?.ToString() is { } text)
                        yield return text;
                }

                break;

            case not null:
                if (value.ToString() is { } single)
                    yield return single;

                break;
        }
    }

    private static async Task<(ChannelsViewModel Vm, FakeMediator Mediator)> CreateLoadedAsync()
    {
        IReadOnlyList<ChannelDto> channels = [Existing];

        var mediator = new FakeMediator()
            .Answer<GetChannelsQuery>(channels)
            .Answer<SaveChannelCommand>(Existing.Id);

        var vm = new ChannelsViewModel(mediator);
        await vm.LoadCommand.ExecuteAsync(null);

        return (vm, mediator);
    }
}
