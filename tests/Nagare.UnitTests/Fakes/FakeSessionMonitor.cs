using Nagare.Application.Abstractions;
using Nagare.Presentation.Abstractions;

namespace Nagare.UnitTests.Fakes;

/// <summary>
/// Stands in for the coordinator's <see cref="ISessionMonitor"/> role. Lets a test raise the two
/// events the real one raises from background threads, and — crucially — tells whether anyone is
/// still subscribed, which is how the leak test proves the ViewModel let go.
/// </summary>
public sealed class FakeSessionMonitor : ISessionMonitor
{
    private readonly List<string> _logs = [];

    public SessionSnapshot? Current { get; set; }

    public event Action<SessionSnapshot>? Changed;
    public event Action<string>? LogAppended;

    public bool HasSubscribers => Changed is not null || LogAppended is not null;

    public void SeedLogs(params string[] lines) => _logs.AddRange(lines);

    public IReadOnlyList<string> RecentLogs(int maxLines)
        => [.. _logs.TakeLast(maxLines)];

    public void RaiseChanged(SessionSnapshot snapshot) => Changed?.Invoke(snapshot);

    public void RaiseLog(string line) => LogAppended?.Invoke(line);
}

/// <summary>Video picker returning a canned path (or null = user cancelled).</summary>
public sealed class FakeVideoFilePicker(string? path) : IVideoFilePicker
{
    public Task<string?> PickAsync() => Task.FromResult(path);
}
