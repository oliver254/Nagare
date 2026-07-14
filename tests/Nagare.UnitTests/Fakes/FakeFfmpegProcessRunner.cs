using Nagare.Application.Abstractions;

namespace Nagare.UnitTests.Fakes;

/// <summary>
/// Manual fake of <see cref="IFfmpegProcessRunner"/> (no mock framework). Drivable: the test
/// decides when the process emits stats, log lines or exits. <see cref="StartFailure"/> makes
/// <see cref="StartAsync"/> throw, reproducing the Win32Exception that
/// <c>Process.Start()</c> raises when the ffmpeg binary is missing or locked.
/// </summary>
public sealed class FakeFfmpegProcessRunner : IFfmpegProcessRunner
{
    /// <summary>Set to make the next <see cref="StartAsync"/> throw (missing binary, locked file...).</summary>
    public Exception? StartFailure { get; set; }

    public FfmpegCommand? StartedCommand { get; private set; }
    public int StartCallCount { get; private set; }
    public int StopCallCount { get; private set; }
    public int DisposeCallCount { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action<string>? OutputLineReceived;
    public event Action<FfmpegStats>? StatsReceived;
    public event Action<int>? Exited;

    /// <summary>
    /// The stats handler as it stood while the process was alive. A real stderr reader thread
    /// reads the delegate, then invokes it; the coordinator may unsubscribe in between, but the
    /// call already in flight still lands. Keeping this snapshot lets a test emit the line a dead
    /// process flushes AFTER its exit — see <see cref="EmitTrailingStats"/>.
    /// </summary>
    private Action<FfmpegStats>? _readerThreadStatsHandler;

    public Task StartAsync(FfmpegCommand command, CancellationToken ct)
    {
        StartCallCount++;
        ct.ThrowIfCancellationRequested();

        if (StartFailure is not null)
            throw StartFailure;   // Process.Start() throws synchronously

        StartedCommand = command;
        IsRunning = true;
        _readerThreadStatsHandler = StatsReceived;   // what the stderr readers hold on to
        return Task.CompletedTask;
    }

    public Task StopAsync(TimeSpan gracePeriod, CancellationToken ct)
    {
        StopCallCount++;
        IsRunning = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        IsRunning = false;
        return ValueTask.CompletedTask;
    }

    // ------------------------------------------------------------------ test drivers

    /// <summary>Simulates a progression line parsed out of stderr.</summary>
    public void EmitStats(FfmpegStats stats) => StatsReceived?.Invoke(stats);

    /// <summary>
    /// Simulates a progression line that the stderr readers flush AFTER the process died: it goes
    /// through the handler captured while it was alive, even though the coordinator has already
    /// unsubscribed from this runner. This is the race that used to fake a recovery.
    /// </summary>
    public void EmitTrailingStats(FfmpegStats stats) => _readerThreadStatsHandler?.Invoke(stats);

    /// <summary>Simulates a (already scrubbed) stderr line.</summary>
    public void EmitOutputLine(string line) => OutputLineReceived?.Invoke(line);

    /// <summary>Simulates the death of the ffmpeg process.</summary>
    public void EmitExit(int exitCode)
    {
        IsRunning = false;
        Exited?.Invoke(exitCode);
    }
}

/// <summary>
/// Hands out <see cref="FakeFfmpegProcessRunner"/> instances and remembers them, so a test can
/// count the launches (a relaunch = a new runner) and drive the last one. <see cref="Configure"/>
/// lets a test arm a specific launch to fail.
/// </summary>
public sealed class FakeFfmpegProcessRunnerFactory : IFfmpegProcessRunnerFactory
{
    private readonly object _sync = new();
    private readonly List<FakeFfmpegProcessRunner> _created = [];

    /// <summary>Called with each fresh runner and its 1-based launch number, before it is started.</summary>
    public Action<FakeFfmpegProcessRunner, int>? Configure { get; set; }

    /// <summary>Number of ffmpeg launches requested by the coordinator.</summary>
    public int CreateCount
    {
        get
        {
            lock (_sync)
                return _created.Count;
        }
    }

    /// <summary>The runner of the n-th launch (1-based).</summary>
    public FakeFfmpegProcessRunner Runner(int launchNumber)
    {
        lock (_sync)
            return _created[launchNumber - 1];
    }

    public IFfmpegProcessRunner Create()
    {
        FakeFfmpegProcessRunner runner;
        int launchNumber;

        lock (_sync)
        {
            runner = new FakeFfmpegProcessRunner();
            _created.Add(runner);
            launchNumber = _created.Count;
        }

        Configure?.Invoke(runner, launchNumber);
        return runner;
    }
}
