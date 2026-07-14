using Monbsoft.BrilliantMediator.Abstractions.Commands;
using Nagare.Application.Abstractions;
using Nagare.Domain.Common;

namespace Nagare.Application.Profiles;

/// <summary>Deletes a profile. Refused if used by the active session (ARCHITECTURE.md §3.2).</summary>
public sealed record DeleteStreamProfileCommand(ProfileId Id) : ICommand;

public sealed class DeleteStreamProfileHandler(
    IStreamProfileRepository repository,
    IStreamSessionCoordinator coordinator)
    : ICommandHandler<DeleteStreamProfileCommand>
{
    public async Task Handle(DeleteStreamProfileCommand command, CancellationToken ct = default)
    {
        if (coordinator.HasActiveSession)
            throw new DomainException("A profile cannot be deleted while a session is active.");

        await repository.DeleteAsync(command.Id, ct);
    }
}
