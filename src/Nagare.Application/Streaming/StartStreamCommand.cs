using Monbsoft.BrilliantMediator.Abstractions.Commands;
using Nagare.Application.Abstractions;
using Nagare.Domain.Common;

namespace Nagare.Application.Streaming;

/// <summary>
/// Starts a broadcast (ARCHITECTURE.md §3.2). Delegates to the coordinator; refused
/// if a session is already active.
/// </summary>
/// <param name="MaxDuration">Maximum broadcast duration, null = no limit. A TimeSpan and not a
/// number of hours: entering "0,5" for half an hour is a UI affair, the model handles a duration.
/// Its bounds are validated by the domain (ADR-0009, S1-S2), never restated here.</param>
public sealed record StartStreamCommand(
    ProfileId ProfileId,
    ChannelId ChannelId,
    string InputFilePath,
    TimeSpan? MaxDuration)
    : ICommand<SessionId>;

public sealed class StartStreamHandler(IStreamSessionCoordinator coordinator)
    : ICommandHandler<StartStreamCommand, SessionId>
{
    public Task<SessionId> Handle(StartStreamCommand command, CancellationToken ct = default)
        => coordinator.StartAsync(
            command.ProfileId, command.ChannelId, command.InputFilePath, command.MaxDuration, ct);
}
