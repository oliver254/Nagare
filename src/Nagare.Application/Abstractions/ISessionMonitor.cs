namespace Nagare.Application.Abstractions;

/// <summary>
/// UI monitoring port (ARCHITECTURE.md §4.4). Implemented by the
/// StreamSessionCoordinator (§5). The ViewModels subscribe and marshal to the UI thread
/// (DispatcherQueue.TryEnqueue — ADR-0006), then unsubscribe on Dispose. Events are raised from
/// the ffmpeg stderr reader thread: never touch the UI directly from them.
/// </summary>
public interface ISessionMonitor
{
    SessionSnapshot? Current { get; }
    IReadOnlyList<string> RecentLogs(int maxLines);
    event Action<SessionSnapshot> Changed;   // transitions + stats (throttled by the coordinator)
    event Action<string> LogAppended;
}
