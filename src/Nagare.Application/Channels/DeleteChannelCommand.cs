using Monbsoft.BrilliantMediator.Abstractions.Commands;
using Nagare.Application.Abstractions;
using Nagare.Domain.Common;

namespace Nagare.Application.Channels;

/// <summary>Deletes a channel. Refused if a session is active (ARCHITECTURE.md §3.2).</summary>
public sealed record DeleteChannelCommand(ChannelId Id) : ICommand;

public sealed class DeleteChannelHandler(
    IChannelRepository repository,
    IStreamSessionCoordinator coordinator)
    : ICommandHandler<DeleteChannelCommand>
{
    public async Task Handle(DeleteChannelCommand command, CancellationToken ct = default)
    {
        if (coordinator.HasActiveSession)
            throw new DomainException("A channel cannot be deleted while a session is active.");

        await repository.DeleteAsync(command.Id, ct);
    }
}
