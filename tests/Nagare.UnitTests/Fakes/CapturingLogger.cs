using Microsoft.Extensions.Logging;

namespace Nagare.UnitTests.Fakes;

/// <summary>
/// Thread-safe logger that records every formatted message. The coordinator logs from its
/// mailbox loop (a background thread), so capture must be synchronized.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<string> _messages = [];
    private readonly object _lock = new();

    public IReadOnlyList<string> Messages
    {
        get { lock (_lock) return [.. _messages]; }
    }

    public int CountOf(string message)
    {
        lock (_lock)
            return _messages.Count(m => m == message);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_lock)
            _messages.Add(formatter(state, exception));
    }
}
