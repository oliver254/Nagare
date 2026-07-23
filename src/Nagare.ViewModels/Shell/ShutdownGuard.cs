namespace Nagare.ViewModels.Shell;

/// <summary>
/// Sequences the application shutdown against the close requests of the main window (SPEC §5).
///
/// <para>The rule it enforces: <b>ffmpeg must never survive the window</b>. The shutdown is
/// asynchronous and takes seconds — a grace period on ffmpeg, then the host's own shutdown timeout —
/// during which the window stays clickable. So every close request is refused until the shutdown has
/// actually finished, and the only close that goes through is the one this class triggers itself.</para>
///
/// <para><b>Why this lives outside the WinUI project.</b> It used to be three fields and two early
/// returns inline in <c>App.xaml.cs</c>, where nothing can be tested: <c>Nagare.WinApp</c> targets a
/// Windows TFM that <c>Nagare.UnitTests</c> cannot reference. The ordering got it wrong once already
/// — a second click on the cross closed the window for real, leaving ffmpeg broadcasting. A rule the
/// spec calls non-negotiable deserves a test, so the rule moved to where tests can reach it. There is
/// no WinUI type here: the window and the host are two delegates.</para>
/// </summary>
public sealed class ShutdownGuard
{
    private readonly Func<Task> _shutdownAsync;
    private readonly Action _closeWindow;
    private readonly Action<Exception> _onError;

    private bool _started;
    private bool _finished;

    /// <param name="shutdownAsync">Stops and disposes the host. Must reach ffmpeg's kill.</param>
    /// <param name="closeWindow">Closes the window for real. Called exactly once, always.</param>
    /// <param name="onError">
    /// Reports a failed shutdown. Captured HERE, on purpose: resolving a logger from the container
    /// mid-shutdown would hit an already-disposed provider, and the throw would escape an async void
    /// — losing the very error being reported, and leaving the window unclosable forever.
    /// </param>
    public ShutdownGuard(Func<Task> shutdownAsync, Action closeWindow, Action<Exception> onError)
    {
        _shutdownAsync = shutdownAsync;
        _closeWindow = closeWindow;
        _onError = onError;
    }

    /// <summary>
    /// Handles one close request. Returns <c>true</c> when the caller must <b>cancel</b> the close.
    ///
    /// <para>Three cases, and the order matters: the shutdown is over (let it through), the shutdown
    /// is running (absorb the click), or nothing started yet (start it, and absorb this click too).</para>
    /// </summary>
    public bool RequestClose()
    {
        if (_finished)
            return false;   // our own closeWindow() coming back through the event — let it go

        if (_started)
            return true;    // already under way: absorb the click, do NOT start a second shutdown

        _started = true;

        // Deliberately not awaited: the caller is a UI event handler that must return NOW so the
        // message loop keeps running. Blocking it is what would deadlock the host's shutdown.
        _ = RunAsync();

        return true;
    }

    private async Task RunAsync()
    {
        try
        {
            await _shutdownAsync();
        }
        catch (Exception ex)
        {
            // A failed shutdown must never keep the window alive: report, then close anyway. An
            // application that refuses to die is worse than one that dies badly.
            try { _onError(ex); } catch { /* reporting must not outrank closing */ }
        }
        finally
        {
            _finished = true;
            _closeWindow();
        }
    }
}
