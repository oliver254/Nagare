using Nagare.Application.Abstractions;

namespace Nagare.Application.Streaming;

/// <summary>Current session snapshot, or null if no active session (ARCHITECTURE.md §3.2).</summary>
public sealed record GetSessionStatusQuery;

public sealed class GetSessionStatusHandler(ISessionMonitor monitor)
    : IQueryHandler<GetSessionStatusQuery, SessionSnapshot?>
{
    public Task<SessionSnapshot?> HandleAsync(GetSessionStatusQuery query, CancellationToken ct)
        => Task.FromResult(monitor.Current);
}
