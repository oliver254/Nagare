using Nagare.Domain.Common;

namespace Nagare.Domain.Sessions;

// Domain events of session transitions (ARCHITECTURE.md §2.5).
// `Reason` is always an ALREADY-scrubbed text: the caller (coordinator) is
// responsible for scrubbing before invoking the transition methods.

public sealed record SessionLaunched(SessionId Id, ProfileId ProfileId, ChannelId ChannelId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record SessionStarted(SessionId Id, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ReconnectStarted(SessionId Id, int Attempt, TimeSpan NextDelay, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record SessionRecovered(SessionId Id, int AfterAttempts, DateTimeOffset OccurredAt) : IDomainEvent;

// SessionStopped.Reason is the one exception to the scrubbing note above: it is a
// SessionStopReason, not a text — there is nothing in it to scrub.
public sealed record SessionStopped(SessionId Id, SessionStopReason Reason, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record SessionFailed(SessionId Id, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
