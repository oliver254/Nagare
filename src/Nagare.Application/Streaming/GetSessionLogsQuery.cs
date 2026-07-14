using Nagare.Application.Abstractions;

namespace Nagare.Application.Streaming;

/// <summary>Last log lines (already scrubbed) of the active session (ARCHITECTURE.md §3.2).</summary>
public sealed record GetSessionLogsQuery(int MaxLines);

public sealed class GetSessionLogsHandler(ISessionMonitor monitor)
    : IQueryHandler<GetSessionLogsQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> HandleAsync(GetSessionLogsQuery query, CancellationToken ct)
        => Task.FromResult(monitor.RecentLogs(query.MaxLines));
}
