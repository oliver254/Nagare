namespace Nagare.Domain.Channels;

public enum Platform { Twitch, YouTube, CustomRtmp }

/// <summary>
/// Default base URLs — UI suggestions, not invariants (ARCHITECTURE.md §2.3):
/// the user may edit them.
/// </summary>
public static class PlatformDefaults
{
    public const string TwitchBaseUrl = "rtmp://live.twitch.tv/app";
    public const string YouTubeBaseUrl = "rtmp://a.rtmp.youtube.com/live2";

    public static string? DefaultBaseUrl(Platform platform) => platform switch
    {
        Platform.Twitch => TwitchBaseUrl,
        Platform.YouTube => YouTubeBaseUrl,
        _ => null
    };
}
