using Nagare.ViewModels.Abstractions;

namespace Nagare.UnitTests.Fakes;

/// <summary>
/// Stands in for the WinUI DispatcherQueue.
///
/// Two modes, and the difference matters:
/// <list type="bullet">
/// <item><b>Inline</b> (default) — the work runs as it is posted. Convenient, and enough to count
/// posts: 10 stats snapshots for 1 post is what proves the throttle.</item>
/// <item><b>Deferred</b> — the work is QUEUED until <see cref="Pump"/> runs it, like a real UI
/// thread that is busy elsewhere. This is the only mode that can tell "marshalled" apart from "not
/// marshalled": inline, a view model mutating its ObservableCollection straight from the ffmpeg
/// stderr reader thread looks exactly like a correct one — and in a real window it would be an
/// RPC_E_WRONG_THREAD or a silent corruption. Deferred mode is also the only way to observe the
/// COALESCING of the log drain, which is the actual anti-freeze mechanism: N lines must schedule
/// ONE callback, not N.</item>
/// </list>
/// (Inline-only, a mutation removing every Post() from the log path left all 24 view-model tests
/// green. The code was right; the net had a hole.)
/// </summary>
public sealed class FakeUiDispatcher : IUiDispatcher
{
    private readonly Queue<Action> _pending = new();

    /// <summary>When false, simulates a closed window: Post refuses the work.</summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>When true, posted work waits for <see cref="Pump"/> instead of running immediately.</summary>
    public bool Deferred { get; set; }

    public int PostCount { get; private set; }

    /// <summary>Work posted but not yet run. In deferred mode only — inline work never waits.</summary>
    public int PendingCount => _pending.Count;

    public bool Post(Action action)
    {
        if (!IsAlive)
            return false;

        PostCount++;

        if (Deferred)
            _pending.Enqueue(action);
        else
            action();

        return true;
    }

    /// <summary>Runs the queued work, as the UI thread would when it gets its turn.</summary>
    public int Pump()
    {
        var ran = 0;

        // Draining may schedule more work (a line arriving mid-drain): snapshot each round.
        while (_pending.Count > 0)
        {
            var round = _pending.ToArray();
            _pending.Clear();

            foreach (var action in round)
            {
                action();
                ran++;
            }
        }

        return ran;
    }
}
