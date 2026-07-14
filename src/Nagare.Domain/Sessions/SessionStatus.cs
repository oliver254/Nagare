namespace Nagare.Domain.Sessions;

public enum SessionStatus
{
    Starting,
    Running,
    Reconnecting,
    Stopped,   // terminal
    Failed     // terminal
}

public static class SessionStatusExtensions
{
    /// <summary>
    /// A session that still holds the single broadcast slot: anything that is not terminal
    /// (SPEC §5 — one session at a time). This is the predicate the coordinator refuses a second
    /// start on, and the predicate the start preflight reports as
    /// <c>StartBlockReason.SessionAlreadyActive</c>.
    ///
    /// It lives HERE because it was written three times — once in the coordinator
    /// (<c>not (Stopped or Failed)</c>), once in the dashboard ViewModel
    /// (<c>Starting or Running or Reconnecting</c>) — the same set expressed the other way round,
    /// free to drift apart the day a status is added. A new status is now active by default, which
    /// is the safe answer: it takes an explicit decision to declare it terminal.
    /// </summary>
    public static bool IsActive(this SessionStatus status)
        => status is not (SessionStatus.Stopped or SessionStatus.Failed);
}
