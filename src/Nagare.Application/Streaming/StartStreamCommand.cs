using Monbsoft.BrilliantMediator.Abstractions.Commands;
using Nagare.Application.Abstractions;
using Nagare.Domain.Common;

namespace Nagare.Application.Streaming;

/// <summary>
/// Starts a broadcast (ARCHITECTURE.md §3.2). Delegates to the coordinator; refused
/// if a session is already active.
/// </summary>
public sealed record StartStreamCommand(ProfileId ProfileId, ChannelId ChannelId, string InputFilePath)
    : ICommand<SessionId>;

public sealed class StartStreamHandler(IStreamSessionCoordinator coordinator)
    : ICommandHandler<StartStreamCommand, SessionId>
{
    public Task<SessionId> Handle(StartStreamCommand command, CancellationToken ct = default)
        => coordinator.StartAsync(command.ProfileId, command.ChannelId, command.InputFilePath, ct);
}
