using Monbsoft.BrilliantMediator.Abstractions;
using Monbsoft.BrilliantMediator.Abstractions.Commands;
using Monbsoft.BrilliantMediator.Abstractions.Events;
using Monbsoft.BrilliantMediator.Abstractions.Queries;

namespace Nagare.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IMediator"/> (no mock framework, like the other fakes here). Each message
/// type is given a canned answer; every dispatched message is recorded, so a test can assert WHAT
/// the ViewModel asked for — the SaveChannelCommand and its null PlaintextKey, typically.
///
/// A message with no registered answer throws: an unexpected dispatch is loud, never silent.
/// </summary>
public sealed class FakeMediator : IMediator
{
    private readonly Dictionary<Type, Func<object, object?>> _answers = [];

    /// <summary>Everything the ViewModel sent, in order.</summary>
    public List<object> Sent { get; } = [];

    public FakeMediator Answer<TMessage>(Func<TMessage, object?> answer)
    {
        _answers[typeof(TMessage)] = message => answer((TMessage)message);
        return this;
    }

    public FakeMediator Answer<TMessage>(object? answer)
        => Answer<TMessage>(_ => answer);

    /// <summary>The single message of that type that was sent — fails the test if there is not exactly one.</summary>
    public TMessage Single<TMessage>() => Sent.OfType<TMessage>().Single();

    /// <summary>A command without response needs no canned answer — but may be given one that throws.</summary>
    public Task DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        Sent.Add(command!);

        if (_answers.TryGetValue(typeof(TCommand), out var answer))
            answer(command!);

        return Task.CompletedTask;
    }

    public Task<TResponse> DispatchAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResponse>
        => Task.FromResult((TResponse)Invoke(command!)!);

    public Task<TResponse> SendAsync<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResponse>
        => Task.FromResult((TResponse)Invoke(query!)!);

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent
        => throw new NotSupportedException();

    private object? Invoke(object message)
    {
        Sent.Add(message);

        if (!_answers.TryGetValue(message.GetType(), out var answer))
            throw new InvalidOperationException($"No canned answer for {message.GetType().Name}.");

        return answer(message);
    }
}
