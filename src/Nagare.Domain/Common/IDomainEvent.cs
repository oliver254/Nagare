namespace Nagare.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
