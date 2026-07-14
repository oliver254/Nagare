using Nagare.Domain.Channels;
using Nagare.Domain.Common;

namespace Nagare.Application.Channels;

/// <summary>
/// Read model of a <see cref="Channel"/> for the UI (ARCHITECTURE.md §3.2). Never
/// carries the key: only <see cref="KeyConfigured"/>.
/// </summary>
public sealed record ChannelDto(
    ChannelId Id,
    string Name,
    Platform Platform,
    string BaseUrl,
    bool KeyConfigured)
{
    public static ChannelDto From(Channel channel)
        => new(channel.Id, channel.Name, channel.Platform, channel.BaseUrl, KeyConfigured: true);
}
