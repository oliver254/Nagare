namespace Nagare.Application.Streaming;

/// <summary>
/// Wraps an infrastructure failure of a stream operation so that it can cross the boundary to the
/// caller — and therefore to the SCREEN — without carrying a stream key.
///
/// <para>The coordinator already scrubs the reason it stores in <c>StreamSession.LastError</c>, but
/// the very same failure used to be handed to the caller RAW through the command's completion. The
/// view model shows <c>ex.Message</c> in an InfoBar: that path goes straight to the user's screen.
/// No exception reachable today embeds the ffmpeg arguments — the invariant held by LUCK, and that
/// is precisely the reasoning this project refuses elsewhere.</para>
///
/// <para>The original exception is deliberately NOT attached as an inner exception: a single
/// <c>ToString()</c> anywhere in a future view would print its unscrubbed message. The type name and
/// the scrubbed text are kept, which is what a user can act on; the raw exception stays in the logs.
/// <see cref="Nagare.Domain.Common.DomainException"/> is never wrapped — its message is ours, it
/// carries no secret, and the UI relies on its type to tell a validation error from a failure.</para>
/// </summary>
public sealed class StreamOperationException(string scrubbedMessage) : Exception(scrubbedMessage);
