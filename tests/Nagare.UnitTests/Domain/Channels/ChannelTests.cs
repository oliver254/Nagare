using Nagare.Domain.Channels;
using Nagare.Domain.Common;

namespace Nagare.UnitTests.Domain.Channels;

/// <summary>Invariants of the channel aggregate (ARCHITECTURE.md §2.3).</summary>
public sealed class ChannelTests
{
    private static readonly ProtectedStreamKey Key = new("cipher-payload");

    [Theory]
    [InlineData("rtmp://live.twitch.tv/app")]
    [InlineData("rtmps://live.twitch.tv/app")]
    [InlineData("RTMP://live.twitch.tv/app")]   // scheme check is case-insensitive
    public void Create_RtmpBaseUrl_IsAccepted(string baseUrl)
    {
        var channel = Channel.Create("Twitch", Platform.Twitch, baseUrl, Key);

        Assert.Equal(baseUrl, channel.BaseUrl);
        Assert.Equal(Platform.Twitch, channel.Platform);
        Assert.Same(Key, channel.Key);
    }

    [Fact]
    public void Create_NameWithSurroundingWhitespace_IsTrimmed()
        => Assert.Equal("Twitch", Channel.Create("  Twitch  ", Platform.Twitch, "rtmp://ingest/app", Key).Name);

    [Theory]
    [InlineData("http://live.twitch.tv/app")]
    [InlineData("live.twitch.tv/app")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NonRtmpBaseUrl_ThrowsDomainException(string baseUrl)
        => Assert.Throws<DomainException>(() => Channel.Create("Twitch", Platform.Twitch, baseUrl, Key));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_ThrowsDomainException(string name)
        => Assert.Throws<DomainException>(() => Channel.Create(name, Platform.Twitch, "rtmp://ingest/app", Key));

    [Fact]
    public void Create_NullKey_ThrowsDomainException()
        => Assert.Throws<DomainException>(() => Channel.Create("Twitch", Platform.Twitch, "rtmp://ingest/app", null!));

    [Fact]
    public void ReplaceKey_NewKey_SwapsTheProtectedKey()
    {
        var channel = Channel.Create("Twitch", Platform.Twitch, "rtmp://ingest/app", Key);
        var newKey = new ProtectedStreamKey("other-cipher");

        channel.ReplaceKey(newKey);

        Assert.Same(newKey, channel.Key);
    }

    [Fact]
    public void ReplaceKey_NullKey_ThrowsDomainException()
    {
        var channel = Channel.Create("Twitch", Platform.Twitch, "rtmp://ingest/app", Key);

        Assert.Throws<DomainException>(() => channel.ReplaceKey(null!));
    }

    [Fact]
    public void Update_ValidValues_ChangesEverythingButTheKeyAndTheId()
    {
        var channel = Channel.Create("Twitch", Platform.Twitch, "rtmp://ingest/app", Key);
        var id = channel.Id;

        channel.Update("My YouTube", Platform.YouTube, PlatformDefaults.YouTubeBaseUrl);

        Assert.Equal(id, channel.Id);
        Assert.Equal("My YouTube", channel.Name);
        Assert.Equal(Platform.YouTube, channel.Platform);
        Assert.Equal(PlatformDefaults.YouTubeBaseUrl, channel.BaseUrl);
        Assert.Same(Key, channel.Key);
    }

    [Fact]
    public void Restore_PersistedChannel_KeepsTheIdAndRevalidatesInvariants()
    {
        var id = ChannelId.New();

        var channel = Channel.Restore(id, "Twitch", Platform.Twitch, "rtmp://ingest/app", Key);

        Assert.Equal(id, channel.Id);
        Assert.Throws<DomainException>(() => Channel.Restore(id, "Twitch", Platform.Twitch, "ftp://nope", Key));
    }
}
