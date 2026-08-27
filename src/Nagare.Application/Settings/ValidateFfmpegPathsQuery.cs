using Monbsoft.BrilliantMediator.Abstractions.Queries;
using Nagare.Application.Abstractions;

namespace Nagare.Application.Settings;

/// <summary>
/// Runs the environment check on the paths <b>typed in the form</b>, not on the ones in effect
/// (ADR-0010). This is the "Test" button: it must answer for what the user is about to save,
/// otherwise it would keep approving the configuration being replaced.
///
/// <para>An empty field is legitimate — it means "take it from the PATH" — and the probe answers
/// for that case just as well.</para>
/// </summary>
public sealed record ValidateFfmpegPathsQuery(string ExecutablePath, string FfprobePath)
    : IQuery<FfmpegEnvironmentReport>;

public sealed class ValidateFfmpegPathsHandler(IFfmpegEnvironmentProbe probe)
    : IQueryHandler<ValidateFfmpegPathsQuery, FfmpegEnvironmentReport>
{
    public Task<FfmpegEnvironmentReport> Handle(ValidateFfmpegPathsQuery query, CancellationToken ct = default)
        => probe.CheckAsync(FfmpegPathInput.Normalize(query.ExecutablePath, query.FfprobePath), ct);
}
