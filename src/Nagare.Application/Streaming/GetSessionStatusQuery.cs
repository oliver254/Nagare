using Monbsoft.BrilliantMediator.Abstractions.Queries;
using Nagare.Application.Abstractions;

namespace Nagare.Application.Streaming;

/// <summary>Current session snapshot, or null if no active session (ARCHITECTURE.md §3.2).</summary>
public sealed record GetSessionStatusQuery : IQuery<SessionSnapshot?>;

public sealed class GetSessionStatusHandler(ISessionMonitor monitor)
    : IQueryHandler<GetSessionStatusQuery, SessionSnapshot?>
{
    public Task<SessionSnapshot?> Handle(GetSessionStatusQuery query, CancellationToken ct = default)
        => Task.FromResult(monitor.Current);
}
