using Nagare.Application.Abstractions;

namespace Nagare.Application.Media;

/// <summary>Validates a media file via ffprobe (ARCHITECTURE.md §3.2).</summary>
public sealed record ValidateMediaFileQuery(string FilePath);

public sealed class ValidateMediaFileHandler(IFfprobeService ffprobe)
    : IQueryHandler<ValidateMediaFileQuery, MediaValidationResult>
{
    public Task<MediaValidationResult> HandleAsync(ValidateMediaFileQuery query, CancellationToken ct)
        => ffprobe.AnalyzeAsync(query.FilePath, ct);
}
