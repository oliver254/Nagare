using Nagare.Application.Abstractions;
using Nagare.Domain.Common;

namespace Nagare.Application.Profiles;

/// <summary>Deletes a profile. Refused if used by the active session (ARCHITECTURE.md §3.2).</summary>
public sealed record DeleteStreamProfileCommand(ProfileId Id);

public sealed class DeleteStreamProfileHandler(
    IStreamProfileRepository repository,
    IStreamSessionCoordinator coordinator)
    : ICommandHandler<DeleteStreamProfileCommand>
{
    public async Task HandleAsync(DeleteStreamProfileCommand command, CancellationToken ct)
    {
        if (coordinator.HasActiveSession)
            throw new DomainException("A profile cannot be deleted while a session is active.");

        await repository.DeleteAsync(command.Id, ct);
    }
}
