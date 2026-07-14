namespace Nagare.Domain.Common;

/// <summary>Thrown when a domain invariant is violated.</summary>
public sealed class DomainException(string message) : Exception(message);
