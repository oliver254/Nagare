using Nagare.Application.Abstractions;

namespace Nagare.Application.Profiles;

/// <summary>Lists all encoding profiles (ARCHITECTURE.md §3.2).</summary>
public sealed record GetStreamProfilesQuery;

public sealed class GetStreamProfilesHandler(IStreamProfileRepository repository)
    : IQueryHandler<GetStreamProfilesQuery, IReadOnlyList<StreamProfileDto>>
{
    public async Task<IReadOnlyList<StreamProfileDto>> HandleAsync(GetStreamProfilesQuery query, CancellationToken ct)
    {
        var profiles = await repository.GetAllAsync(ct);
        return [.. profiles.Select(StreamProfileDto.From)];
    }
}
