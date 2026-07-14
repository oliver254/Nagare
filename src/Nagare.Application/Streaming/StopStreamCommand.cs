using Nagare.Application.Abstractions;

namespace Nagare.Application.Streaming;

/// <summary>Stops the (single) active session (ARCHITECTURE.md §3.2).</summary>
public sealed record StopStreamCommand;

public sealed class StopStreamHandler(IStreamSessionCoordinator coordinator)
    : ICommandHandler<StopStreamCommand>
{
    public Task HandleAsync(StopStreamCommand command, CancellationToken ct)
        => coordinator.StopAsync(ct);
}
