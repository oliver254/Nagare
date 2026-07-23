namespace Nagare.Domain.Sessions;

/// <summary>
/// Why a broadcast stopped (ADR-0009). Carried by <see cref="SessionStopped"/> and kept on the
/// aggregate, because "why did this broadcast end?" is answered from the session, not from a
/// memory held beside it.
///
/// A FAILURE is not a stop: a session in <see cref="SessionStatus.Failed"/> keeps
/// <see cref="StreamSession.StopReason"/> at null, and <see cref="SessionFailed"/> says what
/// happened.
/// </summary>
public enum SessionStopReason
{
    /// <summary>The user asked for it.</summary>
    Manual,

    /// <summary>The maximum duration chosen at launch was reached.</summary>
    DurationElapsed
}
