using Nagare.Domain.Common;
using Nagare.Domain.Profiles;

namespace Nagare.Application.Abstractions;

/// <summary>Persistence port for <see cref="StreamProfile"/> (ARCHITECTURE.md §4.1).</summary>
public interface IStreamProfileRepository
{
    Task<IReadOnlyList<StreamProfile>> GetAllAsync(CancellationToken ct);
    Task<StreamProfile?> GetByIdAsync(ProfileId id, CancellationToken ct);
    Task SaveAsync(StreamProfile profile, CancellationToken ct);   // upsert
    Task DeleteAsync(ProfileId id, CancellationToken ct);
}
