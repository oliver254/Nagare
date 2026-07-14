using Nagare.Application.Abstractions;
using Nagare.Domain.Channels;
using Nagare.Domain.Common;

namespace Nagare.Application.Channels;

/// <summary>
/// Upsert of a channel (ARCHITECTURE.md §3.2). Null Id = creation. <see cref="PlaintextKey"/>
/// null = key unchanged (creation requires a key). The plaintext is protected in the
/// handler via <see cref="IStreamKeyProtector"/> then forgotten.
/// </summary>
public sealed record SaveChannelCommand(
    ChannelId? Id,
    string Name,
    Platform Platform,
    string BaseUrl,
    string? PlaintextKey);

public sealed class SaveChannelHandler(
    IChannelRepository repository,
    IStreamKeyProtector keyProtector)
    : ICommandHandler<SaveChannelCommand, ChannelId>
{
    public async Task<ChannelId> HandleAsync(SaveChannelCommand command, CancellationToken ct)
    {
        Channel channel;

        if (command.Id is { } id)
        {
            var existing = await repository.GetByIdAsync(id, ct)
                ?? throw new DomainException($"Channel {id} not found.");
            existing.Update(command.Name, command.Platform, command.BaseUrl);

            if (command.PlaintextKey is { } plaintext)
                existing.ReplaceKey(Protect(plaintext));

            channel = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(command.PlaintextKey))
                throw new DomainException("A stream key is required when creating a channel.");

            channel = Channel.Create(command.Name, command.Platform, command.BaseUrl, Protect(command.PlaintextKey));
        }

        await repository.SaveAsync(channel, ct);
        return channel.Id;
    }

    private ProtectedStreamKey Protect(string plaintextKey) => keyProtector.Protect(plaintextKey);
}
