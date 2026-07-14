using Monbsoft.BrilliantMediator.Abstractions.Queries;
using Nagare.Application.Abstractions;

namespace Nagare.Application.Profiles;

/// <summary>Lists all encoding profiles (ARCHITECTURE.md §3.2).</summary>
public sealed record GetStreamProfilesQuery : IQuery<IReadOnlyList<StreamProfileDto>>;

public sealed class GetStreamProfilesHandler(IStreamProfileRepository repository)
    : IQueryHandler<GetStreamProfilesQuery, IReadOnlyList<StreamProfileDto>>
{
    public async Task<IReadOnlyList<StreamProfileDto>> Handle(GetStreamProfilesQuery query, CancellationToken ct = default)
    {
        var profiles = await repository.GetAllAsync(ct);
        return [.. profiles.Select(StreamProfileDto.From)];
    }
}
