using Nagare.Application.Abstractions;
using Nagare.Domain.Common;
using Nagare.Domain.Profiles;

namespace Nagare.Application.Profiles;

/// <summary>Upsert of an encoding profile (ARCHITECTURE.md §3.2). Null Id = creation.</summary>
public sealed record SaveStreamProfileCommand(
    ProfileId? Id,
    string Name,
    EncodingSettings Video,
    AudioSettings Audio,
    InputOptions Input);

public sealed class SaveStreamProfileHandler(IStreamProfileRepository repository)
    : ICommandHandler<SaveStreamProfileCommand, ProfileId>
{
    public async Task<ProfileId> HandleAsync(SaveStreamProfileCommand command, CancellationToken ct)
    {
        StreamProfile profile;

        if (command.Id is { } id)
        {
            var existing = await repository.GetByIdAsync(id, ct)
                ?? throw new DomainException($"Profile {id} not found.");
            existing.Update(command.Name, command.Video, command.Audio, command.Input);
            profile = existing;
        }
        else
        {
            profile = StreamProfile.Create(command.Name, command.Video, command.Audio, command.Input);
        }

        await repository.SaveAsync(profile, ct);
        return profile.Id;
    }
}
