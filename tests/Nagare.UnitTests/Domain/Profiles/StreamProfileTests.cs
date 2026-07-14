using Nagare.Domain.Common;
using Nagare.Domain.Profiles;

namespace Nagare.UnitTests.Domain.Profiles;

/// <summary>Invariants of the profile aggregate (ARCHITECTURE.md §2.2).</summary>
public sealed class StreamProfileTests
{
    private static EncodingSettings Video() => new(
        VideoCodec.H264Nvenc, "p2", RateControl.Cbr, 3000, 3000, 3000, 60, 60, null, null);

    private static AudioSettings Audio() => new(AudioCodec.Aac, 128, 48000);

    [Fact]
    public void Create_ValidValues_TrimsTheNameAndKeepsTheSettings()
    {
        var video = Video();
        var audio = Audio();

        var profile = StreamProfile.Create("  Twitch 1080p  ", video, audio, InputOptions.Default);

        Assert.Equal("Twitch 1080p", profile.Name);
        Assert.Same(video, profile.Video);
        Assert.Same(audio, profile.Audio);
        Assert.Equal(InputOptions.Default, profile.Input);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_BlankName_ThrowsDomainException(string? name)
        => Assert.Throws<DomainException>(() => StreamProfile.Create(name!, Video(), Audio(), InputOptions.Default));

    [Fact]
    public void Create_NullSettings_ThrowsDomainException()
        => Assert.Throws<DomainException>(() => StreamProfile.Create("Twitch", null!, Audio(), InputOptions.Default));

    [Fact]
    public void Update_NewSettings_ReplacesThemAndKeepsTheId()
    {
        var profile = StreamProfile.Create("Twitch", Video(), Audio(), InputOptions.Default);
        var id = profile.Id;
        var newVideo = new EncodingSettings(
            VideoCodec.Libx264, "fast", RateControl.Vbr, 6000, 9000, 12000, 120, 60, new Resolution(1920, 1080), 60);

        profile.Update("Twitch HQ", newVideo, Audio(), new InputOptions(ReadAtNativeRate: true, LoopInfinitely: false));

        Assert.Equal(id, profile.Id);
        Assert.Equal("Twitch HQ", profile.Name);
        Assert.Same(newVideo, profile.Video);
        Assert.False(profile.Input.LoopInfinitely);
    }

    [Fact]
    public void Restore_PersistedProfile_KeepsTheIdAndRevalidatesTheName()
    {
        var id = ProfileId.New();

        var profile = StreamProfile.Restore(id, "Twitch", Video(), Audio(), InputOptions.Default);

        Assert.Equal(id, profile.Id);
        Assert.Throws<DomainException>(() => StreamProfile.Restore(id, "  ", Video(), Audio(), InputOptions.Default));
    }
}
