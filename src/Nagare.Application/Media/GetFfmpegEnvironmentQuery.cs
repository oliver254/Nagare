using Nagare.Application.Abstractions;

namespace Nagare.Application.Media;

/// <summary>Startup ffmpeg environment check (ARCHITECTURE.md §3.2).</summary>
public sealed record GetFfmpegEnvironmentQuery;

public sealed class GetFfmpegEnvironmentHandler(IFfmpegEnvironmentProbe probe)
    : IQueryHandler<GetFfmpegEnvironmentQuery, FfmpegEnvironmentReport>
{
    public Task<FfmpegEnvironmentReport> HandleAsync(GetFfmpegEnvironmentQuery query, CancellationToken ct)
        => probe.CheckAsync(ct);
}
