using Nagare.ViewModels.Shell;

namespace Nagare.UnitTests.Shell;

/// <summary>
/// The rule under test is SPEC §5: <b>ffmpeg never survives the window</b>.
///
/// <para>Every test here started life as a real defect or as the thing that made one possible. The
/// shutdown is asynchronous and takes seconds; the window stays clickable throughout. Anything that
/// lets a close through before the shutdown has finished kills the message loop, and with it the
/// continuation that reaches ffmpeg's kill — leaving a broadcast running with no application.</para>
/// </summary>
public sealed class ShutdownGuardTests
{
    [Fact]
    public void The_first_close_is_refused_and_starts_the_shutdown()
    {
        var shutdown = new TaskCompletionSource();
        var closed = 0;
        var guard = new ShutdownGuard(() => shutdown.Task, () => closed++, _ => { });

        var cancel = guard.RequestClose();

        Assert.True(cancel);        // the window must NOT close yet
        Assert.Equal(0, closed);    // ... and nothing closed it behind our back
    }

    /// <summary>
    /// The defect this class was extracted for. The guard used to answer "let it go" to any close
    /// once the shutdown had started, so a second click on the cross closed the window for real —
    /// process gone before ffmpeg was killed.
    /// </summary>
    [Fact]
    public void Further_closes_during_the_shutdown_are_refused_too()
    {
        var shutdown = new TaskCompletionSource();
        var closed = 0;
        var guard = new ShutdownGuard(() => shutdown.Task, () => closed++, _ => { });

        guard.RequestClose();

        Assert.True(guard.RequestClose());   // impatient second click
        Assert.True(guard.RequestClose());   // and a third
        Assert.Equal(0, closed);
    }

    [Fact]
    public async Task The_shutdown_runs_once_however_many_times_the_user_clicks()
    {
        var starts = 0;
        var shutdown = new TaskCompletionSource();
        var guard = new ShutdownGuard(() => { starts++; return shutdown.Task; }, () => { }, _ => { });

        guard.RequestClose();
        guard.RequestClose();
        guard.RequestClose();

        shutdown.SetResult();
        await Task.Yield();

        Assert.Equal(1, starts);   // a second StopAsync on the same host is not a harmless no-op
    }

    [Fact]
    public async Task The_window_closes_once_the_shutdown_has_finished()
    {
        var shutdown = new TaskCompletionSource();
        var closed = 0;
        var guard = new ShutdownGuard(() => shutdown.Task, () => closed++, _ => { });

        guard.RequestClose();
        Assert.Equal(0, closed);

        shutdown.SetResult();
        await Task.Yield();

        Assert.Equal(1, closed);
    }

    /// <summary>
    /// The close the guard triggers itself comes back through the same window event. If it were
    /// refused like any other, the application would refuse to die.
    /// </summary>
    [Fact]
    public async Task The_close_that_follows_the_shutdown_is_allowed_through()
    {
        var shutdown = new TaskCompletionSource();
        var guard = new ShutdownGuard(() => shutdown.Task, () => { }, _ => { });

        guard.RequestClose();
        shutdown.SetResult();
        await Task.Yield();

        Assert.False(guard.RequestClose());
    }

    /// <summary>
    /// A shutdown that throws must still close the window: an application that refuses to die is
    /// worse than one that dies badly. ffmpeg is already dealt with by the host's disposal.
    /// </summary>
    [Fact]
    public async Task A_failed_shutdown_is_reported_and_still_closes_the_window()
    {
        var boom = new InvalidOperationException("host refused to stop");
        var closed = 0;
        Exception? reported = null;
        var guard = new ShutdownGuard(() => Task.FromException(boom), () => closed++, ex => reported = ex);

        guard.RequestClose();
        await Task.Yield();

        Assert.Same(boom, reported);
        Assert.Equal(1, closed);
        Assert.False(guard.RequestClose());
    }

    /// <summary>
    /// The regression that hid inside the previous fix: reporting the error used to resolve a logger
    /// from an already-disposed container. The throw escaped an async void BEFORE the window was
    /// closed, so the window could never be closed again — and ffmpeg outlived it.
    /// </summary>
    [Fact]
    public async Task A_reporter_that_throws_does_not_keep_the_window_alive()
    {
        var closed = 0;
        var guard = new ShutdownGuard(
            () => Task.FromException(new InvalidOperationException("host refused to stop")),
            () => closed++,
            _ => throw new ObjectDisposedException("ServiceProvider"));

        guard.RequestClose();
        await Task.Yield();

        Assert.Equal(1, closed);
        Assert.False(guard.RequestClose());
    }

    /// <summary>
    /// A shutdown that fails synchronously (before its first await) must not leave the guard wedged
    /// half-way: the window still has to close, and the next close must be let through.
    /// </summary>
    [Fact]
    public void A_shutdown_that_throws_synchronously_still_closes_the_window()
    {
        var closed = 0;
        var guard = new ShutdownGuard(
            () => throw new InvalidOperationException("thrown before the first await"),
            () => closed++,
            _ => { });

        guard.RequestClose();

        Assert.Equal(1, closed);
    }
}
