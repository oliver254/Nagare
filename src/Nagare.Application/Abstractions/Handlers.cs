namespace Nagare.Application.Abstractions;

/// <summary>CQRS command handler without a result (ADR-0003).</summary>
public interface ICommandHandler<in TCommand>
{
    Task HandleAsync(TCommand command, CancellationToken ct);
}

/// <summary>CQRS command handler returning a result (ADR-0003).</summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct);
}

/// <summary>CQRS query handler (ADR-0003).</summary>
public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct);
}
