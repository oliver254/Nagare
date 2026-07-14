using Monbsoft.BrilliantMediator.Abstractions.Queries;
using Nagare.Application.Abstractions;
using Nagare.Domain.Common;

namespace Nagare.Application.Streaming;

/// <summary>
/// Builds the masked command line to preview before launching (ARCHITECTURE.md §3.2).
/// Returns <see cref="FfmpegCommand.MaskedCommandLine"/> — the key is never in clear.
/// </summary>
public sealed record BuildCommandPreviewQuery(ProfileId ProfileId, ChannelId ChannelId, string InputFilePath)
    : IQuery<string>;

public sealed class BuildCommandPreviewHandler(
    IStreamProfileRepository profiles,
    IChannelRepository channels,
    IFfmpegCommandBuilder builder)
    : IQueryHandler<BuildCommandPreviewQuery, string>
{
    public async Task<string> Handle(BuildCommandPreviewQuery query, CancellationToken ct = default)
    {
        var profile = await profiles.GetByIdAsync(query.ProfileId, ct)
            ?? throw new DomainException($"Profile {query.ProfileId} not found.");
        var channel = await channels.GetByIdAsync(query.ChannelId, ct)
            ?? throw new DomainException($"Channel {query.ChannelId} not found.");

        var command = builder.Build(profile, channel, query.InputFilePath);
        return command.MaskedCommandLine;
    }
}
