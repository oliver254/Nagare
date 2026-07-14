using Monbsoft.BrilliantMediator.Abstractions.Queries;
using Nagare.Application.Abstractions;

namespace Nagare.Application.Channels;

/// <summary>Lists all channels (ARCHITECTURE.md §3.2). The DTO never carries the key.</summary>
public sealed record GetChannelsQuery : IQuery<IReadOnlyList<ChannelDto>>;

public sealed class GetChannelsHandler(IChannelRepository repository)
    : IQueryHandler<GetChannelsQuery, IReadOnlyList<ChannelDto>>
{
    public async Task<IReadOnlyList<ChannelDto>> Handle(GetChannelsQuery query, CancellationToken ct = default)
    {
        var channels = await repository.GetAllAsync(ct);
        return [.. channels.Select(ChannelDto.From)];
    }
}
