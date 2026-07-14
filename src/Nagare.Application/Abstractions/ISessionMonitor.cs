namespace Nagare.Application.Abstractions;

/// <summary>
/// UI monitoring port (ARCHITECTURE.md §4.4). Implemented by the
/// StreamSessionCoordinator (§5). Blazor pages subscribe (InvokeAsync(StateHasChanged))
/// and unsubscribe on Dispose.
/// </summary>
public interface ISessionMonitor
{
    SessionSnapshot? Current { get; }
    IReadOnlyList<string> RecentLogs(int maxLines);
    event Action<SessionSnapshot> Changed;   // transitions + stats (throttled by the coordinator)
    event Action<string> LogAppended;
}
