using Monbsoft.BrilliantMediator.Abstractions.Queries;
using Nagare.Application.Abstractions;

namespace Nagare.Application.Settings;

/// <summary>
/// Looks for the binaries in the usual install locations (ADR-0010). Detecting does NOT
/// configure anything: the result is a proposal the user still has to save.
/// </summary>
public sealed record DetectFfmpegQuery : IQuery<FfmpegPathSettings>;

public sealed class DetectFfmpegHandler(IFfmpegLocator locator)
    : IQueryHandler<DetectFfmpegQuery, FfmpegPathSettings>
{
    public Task<FfmpegPathSettings> Handle(DetectFfmpegQuery query, CancellationToken ct = default)
        => locator.LocateAsync(ct);
}
