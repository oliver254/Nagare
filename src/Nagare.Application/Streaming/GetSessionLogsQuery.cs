using Monbsoft.BrilliantMediator.Abstractions.Queries;
using Nagare.Application.Abstractions;

namespace Nagare.Application.Streaming;

/// <summary>Last log lines (already scrubbed) of the active session (ARCHITECTURE.md §3.2).</summary>
public sealed record GetSessionLogsQuery(int MaxLines) : IQuery<IReadOnlyList<string>>;

public sealed class GetSessionLogsHandler(ISessionMonitor monitor)
    : IQueryHandler<GetSessionLogsQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(GetSessionLogsQuery query, CancellationToken ct = default)
        => Task.FromResult(monitor.RecentLogs(query.MaxLines));
}
