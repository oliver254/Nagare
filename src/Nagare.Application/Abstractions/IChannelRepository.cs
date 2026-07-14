using Nagare.Domain.Channels;
using Nagare.Domain.Common;

namespace Nagare.Application.Abstractions;

/// <summary>Persistence port for <see cref="Channel"/> (ARCHITECTURE.md §4.1).</summary>
public interface IChannelRepository
{
    Task<IReadOnlyList<Channel>> GetAllAsync(CancellationToken ct);
    Task<Channel?> GetByIdAsync(ChannelId id, CancellationToken ct);
    Task SaveAsync(Channel channel, CancellationToken ct);   // upsert
    Task DeleteAsync(ChannelId id, CancellationToken ct);
}
