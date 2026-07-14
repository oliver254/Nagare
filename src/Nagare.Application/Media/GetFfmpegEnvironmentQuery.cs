using Monbsoft.BrilliantMediator.Abstractions.Queries;
using Nagare.Application.Abstractions;

namespace Nagare.Application.Media;

/// <summary>Startup ffmpeg environment check (ARCHITECTURE.md §3.2).</summary>
public sealed record GetFfmpegEnvironmentQuery : IQuery<FfmpegEnvironmentReport>;

public sealed class GetFfmpegEnvironmentHandler(IFfmpegEnvironmentProbe probe)
    : IQueryHandler<GetFfmpegEnvironmentQuery, FfmpegEnvironmentReport>
{
    public Task<FfmpegEnvironmentReport> Handle(GetFfmpegEnvironmentQuery query, CancellationToken ct = default)
        => probe.CheckAsync(ct);
}
