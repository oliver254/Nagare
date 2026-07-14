using Monbsoft.BrilliantMediator.Abstractions.Queries;
using Nagare.Application.Abstractions;

namespace Nagare.Application.Media;

/// <summary>Validates a media file via ffprobe (ARCHITECTURE.md §3.2).</summary>
public sealed record ValidateMediaFileQuery(string FilePath) : IQuery<MediaValidationResult>;

public sealed class ValidateMediaFileHandler(IFfprobeService ffprobe)
    : IQueryHandler<ValidateMediaFileQuery, MediaValidationResult>
{
    public Task<MediaValidationResult> Handle(ValidateMediaFileQuery query, CancellationToken ct = default)
        => ffprobe.AnalyzeAsync(query.FilePath, ct);
}
