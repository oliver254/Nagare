using Monbsoft.BrilliantMediator.Abstractions.Commands;
using Nagare.Application.Abstractions;

namespace Nagare.Application.Streaming;

/// <summary>Stops the (single) active session (ARCHITECTURE.md §3.2).</summary>
public sealed record StopStreamCommand : ICommand;

public sealed class StopStreamHandler(IStreamSessionCoordinator coordinator)
    : ICommandHandler<StopStreamCommand>
{
    public Task Handle(StopStreamCommand command, CancellationToken ct = default)
        => coordinator.StopAsync(ct);
}
