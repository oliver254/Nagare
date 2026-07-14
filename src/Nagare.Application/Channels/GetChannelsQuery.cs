using Nagare.Application.Abstractions;

namespace Nagare.Application.Channels;

/// <summary>Lists all channels (ARCHITECTURE.md §3.2). The DTO never carries the key.</summary>
public sealed record GetChannelsQuery;

public sealed class GetChannelsHandler(IChannelRepository repository)
    : IQueryHandler<GetChannelsQuery, IReadOnlyList<ChannelDto>>
{
    public async Task<IReadOnlyList<ChannelDto>> HandleAsync(GetChannelsQuery query, CancellationToken ct)
    {
        var channels = await repository.GetAllAsync(ct);
        return [.. channels.Select(ChannelDto.From)];
    }
}
